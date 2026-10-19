using Akka.Actor;
using Akka.Event;
using Akka.Streams;
using Akka.Streams.Dsl;
using Microsoft.Extensions.Options;
using Njord.Core;
using Njord.Core.Actors;
using Njord.Core.Configuration;
using Njord.Core.Configuration.Polling;
using Njord.Core.Domain;
using Njord.Messages.Forecast;
using Njord.Model;
using Servus.Akka;
using Servus.Resilience;

namespace Njord.OpenMeteo;

// Request/response gateway: every FetchDomain ends with exactly one Completed, Failed or Rejected reply.
// The fetch stream is private to this actor and dies with it; forecasts leave through a small branch into the
// pipeline's SinkRef, which is the only part re-attached when the pipeline goes away.
public sealed class OpenMeteoGateway : ReceiveActor, IWithTimers
{
    private const int ForecastBranchSize = 16;
    private const string AttachTimeoutTimer = "attach-timeout";
    private const string AttachRetryTimer = "attach-retry";

    // Ids are unique across incarnations: a restarted gateway keeps its ActorRef, so late offer results, stream
    // signals and branch completions of the previous incarnation must never match anything of the current one.
    private static long LastId;

    private static readonly BackoffPolicy AttachRetryPolicy =
        Backoff.Create(TimeSpan.FromSeconds(1), maxDelay: TimeSpan.FromSeconds(30));

    private sealed record DomainBranchOffered(long RequestId, int Hash, IQueueOfferResult Result);
    private sealed record DomainBranchOfferFailed(long RequestId, Exception Cause);
    private sealed record BranchEnded(long BranchId, Exception? Cause) : IDeadLetterSuppression;
    private sealed record PipelineResolved(IActorRef Pipeline);
    private sealed record PipelineResolveFailed(Exception Cause);
    private sealed record AttachTimedOut(long RequestId);
    private sealed record RetryAttach;

    // Domain fetch done - direct reply without going through the stream pipeline
    private sealed record DomainFetchDone(long RequestId, DomainKind Domain, EnsembleFetchResult Result);
    private sealed record AirQualityFetchDone(long RequestId, AirQualityFetchResult Result);
    private sealed record FloodFetchDone(long RequestId, FloodFetchResult Result);
    private sealed record DomainFetchFailed(long RequestId, DomainKind Domain, Exception Cause);

    private sealed record PendingDomainRequest(IActorRef Requester, Location Location, DomainKind Domain);

    private readonly IOpenMeteoClient _client;
    private readonly TimeProvider _timeProvider;
    private readonly NjordOptions _options;
    private readonly FetchTimeouts _timeouts;
    private readonly int _weight;
    private readonly IGatewayBudgetGate _gate;
    private readonly Dictionary<long, PendingDomainRequest> _pendingDomainRequests = new();

    private IMaterializer? _mat;
    private ISourceQueueWithComplete<DomainDataReceived>? _forecastQueue;
    private IActorRef? _pipeline;
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
        Receive<AirQualityFetchDone>(OnAirQualityFetchDone);
        Receive<FloodFetchDone>(OnFloodFetchDone);
        Receive<DomainFetchFailed>(OnDomainFetchFailure);
        Receive<DomainBranchOffered>(OnDomainBranchOffered);
        Receive<DomainBranchOfferFailed>(msg => DomainReply(msg.RequestId,
            p => new FetchDomainRejected(p.Location, p.Domain, FetchRejectionReason.PipelineUnavailable)));

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
        _log.Info("OpenMeteoGateway started - attaching to the pipeline input");
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

        if (msg.Domain == DomainKind.AirQuality)
        {
            FetchAirQuality(id, location);
            return;
        }

        if (msg.Domain == DomainKind.Flood)
        {
            FetchFlood(id, location);
            return;
        }

        // Weather (Ensemble), AirQuality and Flood are supported. Other domains get a stub rejection.
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
        var domain = msg.Domain;

        _client.FetchEnsembleAsync(location, _options.ForecastDays, now, CancellationToken.None)
            .PipeTo(Self,
                success: result => new DomainFetchDone(id, domain, result),
                failure: ex => new DomainFetchFailed(id, domain, ex));
    }

    private void FetchAirQuality(long id, LocationOptions location)
    {
        if (!_gate.TryAcquire(1))
        {
            DomainReply(id, p => new FetchDomainRejected(p.Location, p.Domain, FetchRejectionReason.Budget));
            return;
        }

        _client.FetchAirQualityAsync(location.Latitude, location.Longitude, _options.ForecastDays, CancellationToken.None)
            .PipeTo(Self,
                success: result => new AirQualityFetchDone(id, result),
                failure: ex => new DomainFetchFailed(id, DomainKind.AirQuality, ex));
    }

    private void OnAirQualityFetchDone(AirQualityFetchDone msg)
    {
        switch (msg.Result)
        {
            case AirQualityFetchResult.Success success:
                OfferDomainData(msg.RequestId, DomainKind.AirQuality, success.Forecast);
                break;
            case AirQualityFetchResult.Failure failure:
                DomainReply(msg.RequestId, p => new FetchDomainFailed(p.Location, p.Domain, failure.Reason, failure.Detail));
                break;
        }
    }

    private void FetchFlood(long id, LocationOptions location)
    {
        if (!_gate.TryAcquire(1))
        {
            DomainReply(id, p => new FetchDomainRejected(p.Location, p.Domain, FetchRejectionReason.Budget));
            return;
        }

        _client.FetchFloodAsync(location.Latitude, location.Longitude, Mapping.OpenMeteoRequestMappings.FloodForecastDays, CancellationToken.None)
            .PipeTo(Self,
                success: result => new FloodFetchDone(id, result),
                failure: ex => new DomainFetchFailed(id, DomainKind.Flood, ex));
    }

    private void OnFloodFetchDone(FloodFetchDone msg)
    {
        switch (msg.Result)
        {
            case FloodFetchResult.Success success:
                OfferDomainData(msg.RequestId, DomainKind.Flood, success.Forecast);
                break;
            case FloodFetchResult.Failure failure:
                DomainReply(msg.RequestId, p => new FetchDomainFailed(p.Location, p.Domain, failure.Reason, failure.Detail));
                break;
        }
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
            _log.Debug("Ignoring forecast SinkRef of request {RequestId}", msg.RequestId);
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
            _log.Debug("Completing the old forecast branch failed: {Message}", ex.Message);
        }

        _forecastQueue = null;
    }

    private void ScheduleAttachRetry(string reason, Exception? cause)
    {
        var delay = AttachRetryPolicy.DelayWithJitter(_attachRetries);
        _attachRetries++;
        _log.Warning(cause, "Not attached to the pipeline input: {Reason} - retrying in {Delay}", reason, delay);
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

    private static long NextId() => Interlocked.Increment(ref LastId);

    internal static int ComputeWeight(int hourlyVariableCount, int forecastDays) =>
        (int)Math.Ceiling(hourlyVariableCount / 10.0) * (int)Math.Ceiling(forecastDays / 14.0);
}
