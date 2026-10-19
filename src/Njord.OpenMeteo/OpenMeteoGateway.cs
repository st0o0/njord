using Akka.Actor;
using Akka.Event;
using Akka.Streams;
using Akka.Streams.Dsl;
using Microsoft.Extensions.Options;
using Njord.Core.Actors;
using Njord.Core.Configuration;
using Njord.Core.Configuration.Polling;
using Njord.Core.Domain;
using Njord.Forecast;
using Njord.Messages.Forecast;
using Njord.Model;
using Servus.Akka;
using Servus.Resilience;
using WeatherForecast = Njord.Forecast.Forecast;

namespace Njord.OpenMeteo;

// Request/response gateway: every FetchDomain ends with exactly one Completed, Failed or Rejected reply.
// Also handles legacy FetchForecast for backward compatibility during transition.
// The fetch stream is private to this actor and dies with it; forecasts leave through a small branch into the
// pipeline's SinkRef, which is the only part re-attached when the pipeline goes away.
public sealed class OpenMeteoGateway : ReceiveActor, IWithTimers
{
    // Holds a full startup round of targets; 64 queued at ~1.5 s per fetch over 2 connections drain within the
    // 60 s admission window.
    private const int FetchQueueSize = 64;
    private const int ForecastBranchSize = 16;
    private const string AttachTimeoutTimer = "attach-timeout";
    private const string AttachRetryTimer = "attach-retry";

    // Ids are unique across incarnations: a restarted gateway keeps its ActorRef, so late offer results, stream
    // signals and branch completions of the previous incarnation must never match anything of the current one.
    private static long LastId;

    private static readonly BackoffPolicy AttachRetryPolicy =
        Backoff.Create(TimeSpan.FromSeconds(1), maxDelay: TimeSpan.FromSeconds(30));

    internal sealed record FetchTarget(
        long RequestId, LocationOptions Location, WeatherModel Model, CycleId Cycle, int Weight, DateTimeOffset Deadline);

    internal sealed record DomainFetchTarget(
        long RequestId, LocationOptions Location, DomainKind Domain, CycleId Cycle, int Weight, DateTimeOffset Deadline);

    internal sealed record Admission(FetchTarget Target, FetchRejectionReason? Rejection);

    private abstract record FetchStreamResult(long RequestId);
    private sealed record FetchDone(long RequestId, PollOutcome Outcome) : FetchStreamResult(RequestId);
    private sealed record FetchRefused(long RequestId, FetchRejectionReason Reason) : FetchStreamResult(RequestId);
    // Stream signals also arrive when the actor stops on purpose; they are not worth a dead-letter warning then.
    private sealed record FetchStreamStopped(long StreamId, Exception? Cause) : IDeadLetterSuppression;
    private sealed record OfferCompleted(long RequestId, IQueueOfferResult Result);
    private sealed record OfferFailed(long RequestId, Exception Cause);
    private sealed record BranchOffered(long RequestId, int Hash, IQueueOfferResult Result);
    private sealed record BranchOfferFailed(long RequestId, Exception Cause);
    private sealed record DomainBranchOffered(long RequestId, int Hash, IQueueOfferResult Result);
    private sealed record DomainBranchOfferFailed(long RequestId, Exception Cause);
    private sealed record BranchEnded(long BranchId, Exception? Cause) : IDeadLetterSuppression;
    private sealed record PipelineResolved(IActorRef Pipeline);
    private sealed record PipelineResolveFailed(Exception Cause);
    private sealed record AttachTimedOut(long RequestId);
    private sealed record RetryAttach;

    // Domain fetch done - direct reply without going through the stream pipeline
    private sealed record DomainFetchDone(long RequestId, DomainKind Domain, EnsembleFetchResult Result);
    private sealed record DomainFetchFailed(long RequestId, DomainKind Domain, Exception Cause);

    // Location and model as the requester spelled them: replies echo them so the requester can correlate.
    private sealed record PendingRequest(IActorRef Requester, LocationId Location, string ModelId);
    private sealed record PendingDomainRequest(IActorRef Requester, LocationId Location, DomainKind Domain);

    private readonly IOpenMeteoClient _client;
    private readonly TimeProvider _timeProvider;
    private readonly NjordOptions _options;
    private readonly FetchTimeouts _timeouts;
    private readonly int _weight;
    private readonly IGatewayBudgetGate _gate;
    private readonly Dictionary<long, PendingRequest> _pendingRequests = new();
    private readonly Dictionary<long, PendingDomainRequest> _pendingDomainRequests = new();

    private IMaterializer? _mat;
    private ISourceQueueWithComplete<FetchTarget>? _fetchQueue;
    private ISourceQueueWithComplete<DomainDataReceived>? _forecastQueue;
    private IActorRef? _pipeline;
    private long _fetchStreamId;
    private long _attachRequestId;
    private long _branchId;
    private int _attachRetries;
    private bool _attachedOnce;

    private readonly ILoggingAdapter _log = Context.GetLogger();

    public OpenMeteoGateway(
        IOpenMeteoClient client,
        TimeProvider timeProvider,
        IOptions<NjordOptions> options,
        ResolvedParameterSet parameters,
        IBudgetProvider budgetProvider,
        FetchTimeouts timeouts)
    {
        _client = client;
        _timeProvider = timeProvider;
        _options = options.Value;
        _timeouts = timeouts;
        _weight = ComputeWeight(parameters.HourlyCount, _options.ForecastDays);

        var budgetTracker = Context.GetActor<IBudgetTracker>();
        _gate = new GatewayBudgetGate(budgetProvider, budgetTracker, timeProvider);

        // Domain-based fetch (new per-location path)
        Receive<FetchDomain>(OnFetchDomain);
        Receive<DomainFetchDone>(OnDomainFetchDone);
        Receive<DomainFetchFailed>(OnDomainFetchFailure);
        Receive<DomainBranchOffered>(OnDomainBranchOffered);
        Receive<DomainBranchOfferFailed>(msg => DomainReply(msg.RequestId,
            p => new FetchDomainRejected(p.Location, p.Domain, FetchRejectionReason.PipelineUnavailable)));

        // Legacy per-model fetch (still used by existing pipeline branch)
        Receive<FetchForecast>(OnFetchForecast);
        Receive<OfferCompleted>(OnOfferCompleted);
        Receive<OfferFailed>(msg => FailRequest(msg.RequestId,
            $"Gateway could not enqueue the request ({msg.Cause.GetType().Name}: {msg.Cause.Message})"));
        Receive<FetchDone>(OnFetchDone);
        Receive<FetchRefused>(msg => Reject(msg.RequestId, msg.Reason));
        Receive<BranchOffered>(OnBranchOffered);
        Receive<BranchOfferFailed>(msg => Reject(msg.RequestId, FetchRejectionReason.PipelineUnavailable));
        Receive<FetchStreamStopped>(OnFetchStreamStopped);

        Receive<PipelineResolved>(OnPipelineResolved);
        Receive<PipelineResolveFailed>(msg => ScheduleAttachRetry("the pipeline could not be resolved", msg.Cause));
        Receive<RequestForecastSinkCompleted>(OnSinkCompleted);
        Receive<RequestForecastSinkFailed>(OnSinkFailed);
        Receive<AttachTimedOut>(OnAttachTimedOut);
        Receive<RetryAttach>(_ => RequestAttach());
        Receive<BranchEnded>(OnBranchEnded);
        Receive<Terminated>(OnTerminated);
    }

    public ITimerScheduler Timers { get; set; } = null!;

    protected override void PreStart()
    {
        _mat = Context.Materializer();
        MaterializeFetchStream();
        _log.Info("OpenMeteoGateway fetch stream materialized - attaching to the pipeline input");
        RequestAttach();
    }

    // ---- Domain-based fetch (new path) ----

    private void OnFetchDomain(FetchDomain msg)
    {
        var location = _options.FindLocation(msg.Location.Name);
        if (location is null)
        {
            Sender.Tell(new FetchDomainFailed(msg.Location, msg.Domain,
                PollFailureReason.ModelUnavailable, $"Location '{msg.Location}' not found in config"));
            return;
        }

        var id = NextId();
        _pendingDomainRequests[id] = new PendingDomainRequest(Sender, msg.Location, msg.Domain);

        // For now, only Weather (Ensemble) is supported. Other domains get a stub rejection.
        if (msg.Domain != DomainKind.Weather)
        {
            DomainReply(id, p => new FetchDomainRejected(p.Location, p.Domain, FetchRejectionReason.PipelineUnavailable));
            return;
        }

        // Budget check via the gate
        if (!_gate.TryAcquire(_weight))
        {
            DomainReply(id, p => new FetchDomainRejected(p.Location, p.Domain, FetchRejectionReason.Budget));
            return;
        }

        var now = _timeProvider.GetUtcNow();
        var cycle = new CycleId(now);
        var domain = msg.Domain;

        _client.FetchEnsembleAsync(location, _options.ForecastDays, cycle, CancellationToken.None)
            .PipeTo(Self,
                success: result => new DomainFetchDone(id, domain, result),
                failure: ex => new DomainFetchFailed(id, domain, ex));
    }

    private void OnDomainFetchDone(DomainFetchDone msg)
    {
        switch (msg.Result)
        {
            case EnsembleFetchResult.Success success:
                OfferDomainData(msg.RequestId, msg.Domain, success.Forecast);
                break;
            case EnsembleFetchResult.Failure failure:
                DomainReply(msg.RequestId, p => new FetchDomainFailed(p.Location, p.Domain, failure.Reason, failure.Detail));
                break;
        }
    }

    private void OfferDomainData(long requestId, DomainKind domain, IDomainForecast forecast)
    {
        if (!_pendingDomainRequests.TryGetValue(requestId, out var pending))
        {
            return;
        }

        if (_forecastQueue is not { } branch)
        {
            DomainReply(requestId, p => new FetchDomainRejected(p.Location, p.Domain, FetchRejectionReason.PipelineUnavailable));
            return;
        }

        var location = _options.FindLocation(pending.Location.Name);
        var data = new DomainDataReceived(
            new Location(location?.Name ?? pending.Location.Name, location?.Latitude ?? 0, location?.Longitude ?? 0), domain, forecast);
        var hash = forecast.GetHashCode();

        Task<IQueueOfferResult> offer;
        try
        {
            offer = branch.OfferAsync(data);
        }
        catch (Exception ex)
        {
            offer = Task.FromException<IQueueOfferResult>(ex);
        }

        offer.PipeTo(Self,
            success: result => new DomainBranchOffered(requestId, hash, result),
            failure: ex => new DomainBranchOfferFailed(requestId, ex));
    }

    private void OnDomainBranchOffered(DomainBranchOffered msg)
    {
        if (msg.Result is QueueOfferResult.Enqueued)
        {
            DomainReply(msg.RequestId, p => new FetchDomainCompleted(p.Location, p.Domain, msg.Hash));
            return;
        }

        DomainReply(msg.RequestId, p => new FetchDomainRejected(p.Location, p.Domain, FetchRejectionReason.PipelineUnavailable));
    }

    private void OnDomainFetchFailure(DomainFetchFailed msg)
    {
        if (_pendingDomainRequests.TryGetValue(msg.RequestId, out var pending))
        {
            _log.Warning(msg.Cause, "Domain fetch for {Location}/{Domain} threw {Type}",
                pending.Location, msg.Domain, msg.Cause.GetType().Name);
        }

        DomainReply(msg.RequestId, p => new Messages.Forecast.FetchDomainFailed(p.Location, p.Domain,
            PollFailureReason.Transport, $"Fetch threw {msg.Cause.GetType().Name}: {msg.Cause.Message}"));
    }

    private void DomainReply(long requestId, Func<PendingDomainRequest, FetchDomainResponse> reply)
    {
        if (_pendingDomainRequests.Remove(requestId, out var pending))
        {
            pending.Requester.Tell(reply(pending));
        }
    }

    // ---- Legacy per-model fetch ----

    private void OnFetchForecast(FetchForecast msg)
    {
        var location = _options.FindLocation(msg.Location.Name);
        if (location is null)
        {
            Sender.Tell(new FetchForecastFailed(msg.Location, msg.ModelId,
                PollFailureReason.ModelUnavailable, $"Location '{msg.Location}' not found in config"));
            return;
        }

        // Without a pipeline input the forecast would be lost, so the request is not even fetched.
        if (_forecastQueue is null || _fetchQueue is null)
        {
            Sender.Tell(new FetchForecastRejected(msg.Location, msg.ModelId, FetchRejectionReason.PipelineUnavailable));
            return;
        }

        var id = NextId();
        var now = _timeProvider.GetUtcNow();
        var target = new FetchTarget(id, location, new WeatherModel(ResolveModel(location, msg.ModelId)),
            new CycleId(now), _weight, now + _timeouts.Admission);
        _pendingRequests[id] = new PendingRequest(Sender, msg.Location, msg.ModelId);

        Task<IQueueOfferResult> offer;
        try
        {
            offer = _fetchQueue.OfferAsync(target);
        }
        catch (Exception ex)
        {
            offer = Task.FromException<IQueueOfferResult>(ex);
        }

        offer.PipeTo(Self,
            success: result => new OfferCompleted(id, result),
            failure: ex => new OfferFailed(id, ex));
    }

    private void OnOfferCompleted(OfferCompleted msg)
    {
        switch (msg.Result)
        {
            case QueueOfferResult.Enqueued:
                return;
            case QueueOfferResult.Failure failure:
                FailRequest(msg.RequestId,
                    $"Gateway could not enqueue the request ({failure.Cause.GetType().Name}: {failure.Cause.Message})");
                return;
            default:
                Reject(msg.RequestId, FetchRejectionReason.QueueFull);
                return;
        }
    }

    private void OnFetchDone(FetchDone msg)
    {
        switch (msg.Outcome)
        {
            case PollOutcome.Success s:
                var hash = ForecastDataHash.Compute(s.Forecast, _timeProvider);
                if (_forecastQueue is null)
                {
                    Reject(msg.RequestId, FetchRejectionReason.PipelineUnavailable);
                    return;
                }

                // The legacy per-model forecast has no place in the domain pipeline: it is acknowledged, not forwarded.
                Reply(msg.RequestId, p => new FetchForecastCompleted(p.Location, p.ModelId, hash));
                break;
            case PollOutcome.Failure f:
                Reply(msg.RequestId, p => new FetchForecastFailed(p.Location, p.ModelId, f.Reason, f.Detail));
                break;
        }
    }

    private void OnBranchOffered(BranchOffered msg)
    {
        if (msg.Result is QueueOfferResult.Enqueued)
        {
            Reply(msg.RequestId, p => new FetchForecastCompleted(p.Location, p.ModelId, msg.Hash));
            return;
        }

        Reject(msg.RequestId, FetchRejectionReason.PipelineUnavailable);
    }

    // The private stream is gone: answer everything still open, then fail so the supervisor restarts the
    // gateway with a fresh stream (FunkArr pattern).
    private void OnFetchStreamStopped(FetchStreamStopped msg)
    {
        if (msg.StreamId != _fetchStreamId)
        {
            return;
        }

        _log.Warning(msg.Cause, "Gateway fetch stream stopped - failing {0} open requests and restarting", _pendingRequests.Count);
        foreach (var id in _pendingRequests.Keys.ToList())
        {
            Reply(id, p => new FetchForecastFailed(p.Location, p.ModelId,
                PollFailureReason.Transport, "Gateway fetch stream stopped"));
        }

        throw new InvalidOperationException("Gateway fetch stream stopped unexpectedly", msg.Cause);
    }

    private void FailRequest(long requestId, string detail)
    {
        if (_pendingRequests.TryGetValue(requestId, out var pending))
        {
            _log.Warning("Fetch request for {0}/{1} failed: {2}", pending.Location, pending.ModelId, detail);
        }

        Reply(requestId, p => new FetchForecastFailed(p.Location, p.ModelId, PollFailureReason.Transport, detail));
    }

    private void Reject(long requestId, FetchRejectionReason reason)
    {
        if (_pendingRequests.TryGetValue(requestId, out var pending))
        {
            _log.Warning("Fetch request for {0}/{1} rejected: {2}", pending.Location, pending.ModelId, reason);
        }

        Reply(requestId, p => new FetchForecastRejected(p.Location, p.ModelId, reason));
    }

    // The only way a queued request is answered: the request is forgotten first, so it is answered at most once.
    private void Reply(long requestId, Func<PendingRequest, FetchForecastResponse> reply)
    {
        if (_pendingRequests.Remove(requestId, out var pending))
        {
            pending.Requester.Tell(reply(pending));
        }
    }

    private void RequestAttach() =>
        Context.GetActorAsync<IForecastPipeline>().PipeTo(Self,
            success: pipeline => new PipelineResolved(pipeline),
            failure: ex => new PipelineResolveFailed(ex));

    private void OnPipelineResolved(PipelineResolved msg)
    {
        _attachRequestId = NextId();
        msg.Pipeline.Tell(new RequestForecastSink(_attachRequestId), Self);
        Timers.StartSingleTimer(AttachTimeoutTimer, new AttachTimedOut(_attachRequestId), _timeouts.Admission);
    }

    private void OnSinkCompleted(RequestForecastSinkCompleted msg)
    {
        if (msg.RequestId != _attachRequestId)
        {
            _log.Debug("Ignoring forecast SinkRef of request {0}", msg.RequestId);
            return;
        }

        _attachRequestId = 0;
        Timers.Cancel(AttachTimeoutTimer);
        _pipeline = Sender;
        Context.Watch(_pipeline);
        AttachForecastBranch(msg.SinkRef);
        _attachRetries = 0;
        if (_attachedOnce)
        {
            Telemetry.PipelineReattaches.Add(1);
        }

        _attachedOnce = true;
        _log.Info("Attached to the pipeline input");
    }

    private void OnSinkFailed(RequestForecastSinkFailed msg)
    {
        if (msg.RequestId != _attachRequestId)
        {
            return;
        }

        _attachRequestId = 0;
        Timers.Cancel(AttachTimeoutTimer);
        ScheduleAttachRetry("the pipeline could not create a SinkRef", msg.Cause);
    }

    private void OnAttachTimedOut(AttachTimedOut msg)
    {
        if (msg.RequestId != _attachRequestId)
        {
            return;
        }

        _attachRequestId = 0;
        ScheduleAttachRetry("the pipeline did not answer the SinkRef request", null);
    }

    private void OnBranchEnded(BranchEnded msg)
    {
        if (msg.BranchId != _branchId)
        {
            return;
        }

        DetachBranch();
        ScheduleAttachRetry("the forecast branch to the pipeline ended", msg.Cause);
    }

    private void OnTerminated(Terminated msg)
    {
        if (!Equals(msg.ActorRef, _pipeline))
        {
            return;
        }

        DetachBranch();
        ScheduleAttachRetry("the pipeline terminated", null);
    }

    private void DetachBranch()
    {
        _branchId = 0;
        if (_pipeline is not null)
        {
            Context.Unwatch(_pipeline);
            _pipeline = null;
        }

        try
        {
            _forecastQueue?.Complete();
        }
        catch (Exception ex)
        {
            _log.Debug("Completing the old forecast branch failed: {0}", ex.Message);
        }

        _forecastQueue = null;
    }

    private void ScheduleAttachRetry(string reason, Exception? cause)
    {
        var delay = AttachRetryPolicy.DelayWithJitter(_attachRetries);
        _attachRetries++;
        _log.Warning(cause, "Not attached to the pipeline input: {0} - retrying in {1}", reason, delay);
        Timers.StartSingleTimer(AttachRetryTimer, new RetryAttach(), delay);
    }

    private void AttachForecastBranch(ISinkRef<DomainDataReceived> sinkRef)
    {
        var branchId = _branchId = NextId();
        var (queue, done) = Source.Queue<DomainDataReceived>(ForecastBranchSize, OverflowStrategy.DropHead)
            .WatchTermination(Keep.Both)
            .To(sinkRef.Sink)
            .Run(_mat!);

        done.PipeTo(Self,
            success: () => new BranchEnded(branchId, null),
            failure: ex => new BranchEnded(branchId, ex));
        _forecastQueue = queue;
    }

    private void MaterializeFetchStream()
    {
        var streamId = _fetchStreamId = NextId();
        _fetchQueue = Source.Queue<FetchTarget>(FetchQueueSize, OverflowStrategy.DropNew)
            .Via(new GatewayThrottleStage(_gate, _timeProvider))
            .SelectAsyncUnordered(2, admission => admission.Rejection is { } reason
                ? Task.FromResult<FetchStreamResult>(new FetchRefused(admission.Target.RequestId, reason))
                : FetchAsync(admission.Target))
            .WithAttributes(ActorAttributes.CreateSupervisionStrategy(StreamSupervision.LoggingDecider(_log)))
            .To(Sink.ActorRef<FetchStreamResult>(Self, new FetchStreamStopped(streamId, null), ex => new FetchStreamStopped(streamId, ex)))
            .Run(_mat!);
    }

    private string ResolveModel(LocationOptions location, string modelId) =>
        _options.Models.Concat(location.Models ?? [])
            .FirstOrDefault(m => m.Equals(modelId, StringComparison.OrdinalIgnoreCase)) ?? modelId;

    // A client that throws instead of returning a PollOutcome still has to answer its requester.
    private async Task<FetchStreamResult> FetchAsync(FetchTarget target)
    {
        try
        {
            var outcome = await _client.FetchAsync(target.Location, target.Model, target.Cycle, CancellationToken.None);
            return new FetchDone(target.RequestId, outcome);
        }
        catch (Exception ex)
        {
            _log.Warning(ex, "Fetch for {0}/{1} threw {2}", target.Location.Name, target.Model.Id, ex.GetType().Name);
            var failure = new PollOutcome.Failure(target.Location.Name, target.Model, PollFailureReason.Transport,
                $"Fetch threw {ex.GetType().Name}: {ex.Message}");
            return new FetchDone(target.RequestId, failure);
        }
    }

    private static long NextId() => Interlocked.Increment(ref LastId);

    internal static int ComputeWeight(int hourlyVariableCount, int forecastDays) =>
        (int)Math.Ceiling(hourlyVariableCount / 10.0) * (int)Math.Ceiling(forecastDays / 14.0);
}
