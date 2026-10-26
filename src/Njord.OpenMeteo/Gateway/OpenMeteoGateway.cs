using Akka.Actor;
using Akka.Event;
using Akka.Streams;
using Akka.Streams.Dsl;
using Microsoft.Extensions.Options;
using Njord.Core;
using Njord.Core.Actors;
using Njord.Core.Configuration;
using Njord.Core.Domain;
using Njord.Core.Polling;
using Njord.Forecast;
using Njord.Messages.Forecast;
using Njord.OpenMeteo.Budget;
using Njord.OpenMeteo.Mapping;
using Servus.Akka;

namespace Njord.OpenMeteo.Gateway;

// Request/response gateway: every FetchDomain ends with exactly one Completed, Failed or Rejected reply.
// Requests run through a bounded stream (budget gate, client call); the actor routes the outcomes.
// The pipeline hubs outlive this actor, so it attaches once in PreStart; a failed or lost attach crashes the
// actor and supervision restarts it.
public sealed class OpenMeteoGateway : ReceiveActor
{
    private const int ForecastBranchSize = 16;
    private const int RequestQueueSize = 64;
    private const int MaxConcurrent = 3;

    // Ids are unique across incarnations: a restarted gateway keeps its ActorRef but gets a fresh instance, so
    // late results of the previous incarnation must never match anything of the current one.
    private static long LastId;
    private static long NextId() => Interlocked.Increment(ref LastId);

    private sealed record PipelineResolved(IActorRef Pipeline);
    private sealed record AttachFailed(string Reason, Exception? Cause);
    private sealed record BranchEnded(long BranchId, Exception? Cause) : IDeadLetterSuppression;
    private sealed record DomainBranchOffered(long RequestId, int Hash, IQueueOfferResult Result);
    private sealed record OfferOutcome(long RequestId, IQueueOfferResult? Result);
    private sealed record StreamFailed(long Generation, Exception Cause);

    private sealed record StreamCompleted(long Generation);

    private sealed record StreamRequest(long RequestId, DomainKind Domain, LocationOptions Location);

    private sealed record FetchOutcome(
        long RequestId,
        FetchResult? Result,
        FetchRejectionReason? Rejected,
        Exception? Error,
        TimeSpan Elapsed);

    private sealed record PendingDomainRequest(IActorRef Requester, DomainKind Domain, Location Resolved);

    private readonly IReadOnlyDictionary<DomainKind, IDomainClient> _clients;
    private readonly TimeProvider _timeProvider;
    private readonly NjordOptions _options;
    private readonly FetchTimeouts _timeouts;
    private readonly IGatewayBudgetGate _gate;
    private readonly Dictionary<long, PendingDomainRequest> _pending = new();
    private readonly ILoggingAdapter _log = Context.GetLogger();

    private ISourceQueueWithComplete<StreamRequest>? _requestQueue;
    private ISourceQueueWithComplete<DomainDataReceived>? _forecastQueue;
    private readonly IMaterializer _materializer = Context.Materializer();
    private long _branchId;
    private readonly long _generation = NextId();

    public OpenMeteoGateway(
        IReadOnlyDictionary<DomainKind, IDomainClient> clients,
        TimeProvider timeProvider,
        IOptions<NjordOptions> options,
        IBudgetProvider budgetProvider,
        FetchTimeouts timeouts)
    {
        _clients = clients;
        _timeProvider = timeProvider;
        _options = options.Value;
        _timeouts = timeouts;
        _gate = new GatewayBudgetGate(budgetProvider, Context.GetActor<IBudgetTracker>(), timeProvider);

        Receive<FetchDomain>(OnFetchDomain);
        Receive<OfferOutcome>(OnOfferOutcome);
        Receive<FetchOutcome>(OnFetchOutcome);
        Receive<DomainBranchOffered>(OnDomainBranchOffered);
        Receive<StreamFailed>(msg => msg.Generation == _generation,
            msg => throw new InvalidOperationException("The fetch stream failed", msg.Cause));
        Receive<StreamCompleted>(msg => msg.Generation == _generation,
            _ => throw new InvalidOperationException("The fetch stream completed unexpectedly"));
        Receive<StreamFailed>(_ => { });
        Receive<StreamCompleted>(_ => { });

        Receive<PipelineResolved>(OnPipelineResolved);
        Receive<RequestForecastSinkCompleted>(OnSinkCompleted);
        Receive<RequestForecastSinkFailed>(msg => throw new InvalidOperationException("The pipeline could not create a SinkRef", msg.Cause));
        Receive<AttachFailed>(msg => throw new InvalidOperationException($"Not attached to the pipeline input: {msg.Reason}", msg.Cause));
        Receive<BranchEnded>(msg => msg.BranchId == _branchId,
            msg => throw new InvalidOperationException("The forecast branch to the pipeline ended", msg.Cause));
        Receive<Terminated>(_ => throw new InvalidOperationException("The pipeline terminated"));
    }

    protected override void PreStart()
    {
        _requestQueue = Source.Queue<StreamRequest>(RequestQueueSize, OverflowStrategy.DropNew)
            .SelectAsyncUnordered(MaxConcurrent, FetchAsync)
            .To(Sink.ActorRef<FetchOutcome>(Self, new StreamCompleted(_generation), ex => new StreamFailed(_generation, ex)))
            .Run(_materializer);

        Context.GetActorAsync<IForecastPipeline>().PipeTo(Self,
            success: pipeline => new PipelineResolved(pipeline),
            failure: ex => new AttachFailed("the pipeline could not be resolved", ex));
    }

    protected override void PostStop()
    {
        _requestQueue?.Complete();
        _forecastQueue?.Complete();
    }

    private async Task<FetchOutcome> FetchAsync(StreamRequest request)
    {
        if (!_clients.TryGetValue(request.Domain, out var client))
        {
            return new FetchOutcome(request.RequestId, null, FetchRejectionReason.PipelineUnavailable, null, TimeSpan.Zero);
        }

        var start = _timeProvider.GetTimestamp();
        try
        {
            if (!_gate.TryAcquire(client.CallWeight))
            {
                return new FetchOutcome(request.RequestId, null, FetchRejectionReason.Budget, null, TimeSpan.Zero);
            }

            var result = await client.FetchAsync(request.Location.Latitude.GetValueOrDefault(), request.Location.Longitude.GetValueOrDefault(),
                _timeProvider.GetUtcNow(), CancellationToken.None);
            return new FetchOutcome(request.RequestId, result, null, null, _timeProvider.GetElapsedTime(start));
        }
        catch (Exception ex)
        {
            return new FetchOutcome(request.RequestId, null, null, ex, _timeProvider.GetElapsedTime(start));
        }
    }

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
        var resolved = new Location(location.Name, location.Latitude.GetValueOrDefault(), location.Longitude.GetValueOrDefault());
        _pending[id] = new PendingDomainRequest(Sender, msg.Domain, resolved);
        _requestQueue!.OfferAsync(new StreamRequest(id, msg.Domain, location)).PipeTo(Self,
            success: result => new OfferOutcome(id, result),
            failure: _ => new OfferOutcome(id, null));
    }

    private void OnOfferOutcome(OfferOutcome msg)
    {
        if (msg.Result is not QueueOfferResult.Enqueued)
        {
            Reply(msg.RequestId, p => new FetchDomainRejected(p.Resolved, p.Domain,
                msg.Result is QueueOfferResult.Dropped ? FetchRejectionReason.QueueFull : FetchRejectionReason.PipelineUnavailable));
        }
    }

    private void OnFetchOutcome(FetchOutcome msg)
    {
        if (!_pending.TryGetValue(msg.RequestId, out var pending))
        {
            return;
        }

        if (msg.Rejected is { } rejection)
        {
            Reply(msg.RequestId, p => new FetchDomainRejected(p.Resolved, p.Domain, rejection));
            return;
        }

        var domainTag = pending.Domain.ToTelemetryTag();
        var location = new KeyValuePair<string, object?>("location", pending.Resolved.Name);
        var domain = new KeyValuePair<string, object?>("domain", domainTag);
        var success = msg.Result is FetchSuccess;
        Telemetry.FetchTotal.Add(1, location, domain, new KeyValuePair<string, object?>("outcome", success ? "success" : "failure"));

        if (msg.Error is { } error)
        {
            _log.Warning(error, "Domain fetch for {Location}/{Domain} threw {Type}", pending.Resolved.Name, pending.Domain, error.GetType().Name);
            Reply(msg.RequestId, p => new FetchDomainFailed(p.Resolved, p.Domain, PollFailureReason.Transport,
                $"Fetch threw {error.GetType().Name}: {error.Message}"));
            return;
        }

        switch (msg.Result)
        {
            case FetchSuccess ok:
                Telemetry.FetchDuration.Record(msg.Elapsed.TotalSeconds, location, domain);
                OfferToPipeline(msg.RequestId, pending, ok);
                break;
            case FetchError failure:
                Reply(msg.RequestId, p => new FetchDomainFailed(p.Resolved, p.Domain, failure.Reason, failure.Message));
                break;
            default:
                Reply(msg.RequestId, p => new FetchDomainFailed(p.Resolved, p.Domain, PollFailureReason.MalformedPayload,
                    $"The {domainTag} endpoint returned no usable data"));
                break;
        }
    }

    private void OfferToPipeline(long requestId, PendingDomainRequest pending, FetchSuccess ok)
    {
        if (_forecastQueue is not { } branch)
        {
            Reply(requestId, p => new FetchDomainRejected(p.Resolved, p.Domain, FetchRejectionReason.PipelineUnavailable));
            return;
        }

        var hash = ok.Forecast.GetHashCode();
        // Per-model data first so the primary forecast is evaluated with all models merged in.
        var offers = ok.Models
            .Select(model => new DomainDataReceived(pending.Resolved, pending.Domain, model.Value, model.Key))
            .Append(new DomainDataReceived(pending.Resolved, pending.Domain, ok.Forecast))
            .ToList();

        OfferAllAsync(branch, offers).PipeTo(Self,
            success: result => new DomainBranchOffered(requestId, hash, result),
            failure: ex => new DomainBranchOffered(requestId, hash, new QueueOfferResult.Failure(ex)));
    }

    private static async Task<IQueueOfferResult> OfferAllAsync(
        ISourceQueueWithComplete<DomainDataReceived> branch,
        IReadOnlyList<DomainDataReceived> offers)
    {
        IQueueOfferResult last = QueueOfferResult.Enqueued.Instance;
        foreach (var offer in offers)
        {
            last = await branch.OfferAsync(offer);
            if (last is not QueueOfferResult.Enqueued)
            {
                return last;
            }
        }

        return last;
    }

    private void OnDomainBranchOffered(DomainBranchOffered msg) =>
        Reply(msg.RequestId, p => msg.Result is QueueOfferResult.Enqueued
            ? new FetchDomainCompleted(p.Resolved, p.Domain, msg.Hash)
            : new FetchDomainRejected(p.Resolved, p.Domain, FetchRejectionReason.PipelineUnavailable));

    private void OnPipelineResolved(PipelineResolved msg)
    {
        Context.Watch(msg.Pipeline);
        msg.Pipeline.Ask<RequestForecastSinkResponse>(new RequestForecastSink(NextId()), _timeouts.Admission).PipeTo(Self,
            success: response => response,
            failure: ex => new AttachFailed("the pipeline did not answer the SinkRef request", ex));
    }

    private void OnSinkCompleted(RequestForecastSinkCompleted msg)
    {
        var branchId = _branchId = NextId();
        var (queue, done) = Source.Queue<DomainDataReceived>(ForecastBranchSize, OverflowStrategy.DropHead)
            .WatchTermination(Keep.Both)
            .To(msg.SinkRef.Sink)
            .Run(_materializer);
        done.PipeTo(Self,
            success: () => new BranchEnded(branchId, null),
            failure: ex => new BranchEnded(branchId, ex));
        _forecastQueue = queue;
        _log.Info("Attached to the pipeline input");
    }

    private void Reply(long requestId, Func<PendingDomainRequest, FetchDomainResponse> reply)
    {
        if (_pending.Remove(requestId, out var p))
        {
            p.Requester.Tell(reply(p));
        }
    }
}
