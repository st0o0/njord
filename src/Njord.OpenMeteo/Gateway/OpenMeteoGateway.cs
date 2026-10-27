using Akka;
using Akka.Actor;
using Akka.Event;
using Akka.Streams;
using Akka.Streams.Dsl;
using Njord.Core;
using Njord.Core.Actors;
using Njord.Core.Domain;
using Njord.Core.Polling;
using Njord.Forecast;
using Njord.Messages.Forecast;
using Njord.OpenMeteo.Budget;
using Njord.OpenMeteo.Mapping;
using Servus.Akka;

namespace Njord.OpenMeteo.Gateway;

// Request/response gateway: every FetchDomain ends with exactly one Completed, Failed or Rejected reply, routed
// through the poll shard region to the owning PollWorker. The pipeline hubs outlive this actor, so it attaches once
// in PreStart; a failed or lost attach crashes the actor and supervision restarts it.
public sealed class OpenMeteoGateway : ReceiveActor
{
    private const int MaxConcurrentFetches = 3;
    private const int QueueSize = 64;
    private const int ForecastBufferSize = 16;

    // A restarted gateway keeps its ActorRef, so a late handshake answer of the previous incarnation must not match.
    private static long AttachCounter;

    private sealed record Offered(FetchDomain Request, IQueueOfferResult Result);

    private static Sink<FetchAttempt, NotUsed> ReplySink(IActorRef pollRegion) =>
        Flow.Create<FetchAttempt>()
            .Select(attempt =>
            {
                var (location, domain) = (attempt.Request.Location, attempt.Request.Domain);
                FetchDomainResponse reply = attempt switch
                {
                    { Rejection: { } rejection } => new FetchDomainRejected(location, domain, rejection),
                    { Error: { } ex } => new FetchDomainFailed(location, domain, PollFailureReason.Transport,
                        $"Fetch threw {ex.GetType().Name}: {ex.Message}"),
                    { Result: FetchSuccess ok } => new FetchDomainCompleted(location, domain, ok.Forecast.GetHashCode()),
                    { Result: FetchError error } => new FetchDomainFailed(location, domain, error.Reason, error.Message),
                    _ => new FetchDomainFailed(location, domain, PollFailureReason.MalformedPayload,
                        $"The {domain.ToTelemetryTag()} endpoint returned no usable data"),
                };
                return reply;
            })
            .To(Sink.ForEach<FetchDomainResponse>(reply => pollRegion.Tell(reply)));

    private sealed record GatedRequest(FetchDomain Request, IDomainClient? Client, FetchRejectionReason? Rejection);

    private sealed record FetchAttempt(FetchDomain Request, FetchResult? Result, FetchRejectionReason? Rejection, Exception? Error);

    private sealed record ResolvedRequest(FetchDomain Request, IDomainClient? Client);

    private readonly IReadOnlyDictionary<DomainKind, IDomainClient> _clients;
    private readonly TimeProvider _timeProvider;
    private readonly IBudgetProvider _budgetProvider;
    private readonly FetchTimeouts _timeouts;
    private readonly int _maxCallWeight;
    private readonly IActorRef _pollRegion;
    private readonly CancellationTokenSource _stopped = new();
    private readonly long _attachId = Interlocked.Increment(ref AttachCounter);
    private readonly ILoggingAdapter _log = Context.GetLogger();

    private ISourceQueueWithComplete<FetchDomain>? _requestQueue;

    public OpenMeteoGateway(
        IReadOnlyDictionary<DomainKind, IDomainClient> clients,
        TimeProvider timeProvider,
        IBudgetProvider budgetProvider,
        FetchTimeouts timeouts)
    {
        _clients = clients;
        _timeProvider = timeProvider;
        _budgetProvider = budgetProvider;
        _timeouts = timeouts;
        _pollRegion = Context.GetActor<IPollRegion>();
        _maxCallWeight = clients.Count > 0 ? clients.Values.Max(c => c.CallWeight) : 1;

        Receive<FetchDomain>(OnFetchDomain);
        Receive<Offered>(OnOffered);
        Receive<RequestForecastSinkCompleted>(msg => msg.RequestId == _attachId, OnSinkCompleted);
        Receive<RequestForecastSinkFailed>(msg => msg.RequestId == _attachId,
            msg => throw new InvalidOperationException("The pipeline could not create a SinkRef", msg.Cause));
        Receive<Terminated>(_ => throw new InvalidOperationException("The pipeline terminated"));
    }

    protected override void PreStart()
    {
        var pipeline = Context.GetActor<IForecastPipeline>();
        Context.Watch(pipeline);
        pipeline.Ask<RequestForecastSinkResponse>(new RequestForecastSink(_attachId), _timeouts.Admission).PipeTo(Self,
            success: response => response,
            failure: ex => new RequestForecastSinkFailed(_attachId, ex));
    }

    protected override void PostStop() => _stopped.Cancel();

    private void OnFetchDomain(FetchDomain msg)
    {
        if (_requestQueue is not { } queue)
        {
            _pollRegion.Tell(new FetchDomainRejected(msg.Location, msg.Domain, FetchRejectionReason.PipelineUnavailable));
            return;
        }

        queue.OfferAsync(msg).PipeTo(Self,
            success: result => new Offered(msg, result),
            failure: ex => new Offered(msg, new QueueOfferResult.Failure(ex)));
    }

    private void OnOffered(Offered msg)
    {
        if (msg.Result is QueueOfferResult.Enqueued)
        {
            return;
        }

        var reason = msg.Result is QueueOfferResult.Dropped
            ? FetchRejectionReason.QueueFull
            : FetchRejectionReason.PipelineUnavailable;
        _pollRegion.Tell(new FetchDomainRejected(msg.Request.Location, msg.Request.Domain, reason));
    }

    private void OnSinkCompleted(RequestForecastSinkCompleted msg)
    {
        var clients = _clients;
        var timeProvider = _timeProvider;
        var stopped = _stopped.Token;
        var pollRegion = _pollRegion;
        var trackerActor = Context.GetActor<IBudgetTracker>();

        _requestQueue = Source.Queue<FetchDomain>(QueueSize, OverflowStrategy.DropNew)
            .Select(request => new ResolvedRequest(request, clients.GetValueOrDefault(request.Domain)))
            .Via(new BudgetGateStage<ResolvedRequest>(
                _budgetProvider, trackerActor, timeProvider,
                r => r.Client?.CallWeight ?? 1, _maxCallWeight))
            .Select(resolved => resolved.Client is null
                ? new GatedRequest(resolved.Request, null, FetchRejectionReason.PipelineUnavailable)
                : new GatedRequest(resolved.Request, resolved.Client, null))
            .SelectAsyncUnordered(MaxConcurrentFetches, async gated =>
            {
                if (gated.Rejection is { } rejection)
                {
                    return new FetchAttempt(gated.Request, null, rejection, null);
                }

                var location = gated.Request.Location;
                var domainTag = gated.Request.Domain.ToTelemetryTag();
                var locationTag = new KeyValuePair<string, object?>("location", location.Name);
                var domainKey = new KeyValuePair<string, object?>("domain", domainTag);
                var sw = System.Diagnostics.Stopwatch.StartNew();
                try
                {
                    var result = await gated.Client!.FetchAsync(location.Latitude, location.Longitude,
                        timeProvider.GetUtcNow(), stopped);
                    var success = result is FetchSuccess;
                    Telemetry.FetchTotal.Add(1, locationTag, domainKey,
                        new KeyValuePair<string, object?>("outcome", success ? "success" : "failure"));
                    if (success)
                    {
                        Telemetry.FetchDuration.Record(sw.Elapsed.TotalSeconds, locationTag, domainKey);
                    }

                    return new FetchAttempt(gated.Request, result, null, null);
                }
                catch (Exception ex)
                {
                    Telemetry.FetchTotal.Add(1, locationTag, domainKey, new KeyValuePair<string, object?>("outcome", "failure"));
                    return new FetchAttempt(gated.Request, null, null, ex);
                }
            })
            // Replies go to the poll region with a plain Tell: Sink.ActorRef would also send its completion or failure marker there, which the shard extractor cannot route.
            .AlsoToMaterialized(ReplySink(pollRegion), Keep.Left)
            .Where(attempt => attempt.Result is FetchSuccess)
            // Per-model data first so the primary forecast is evaluated with all models merged in.
            .SelectMany(attempt =>
            {
                var ok = (FetchSuccess)attempt.Result!;
                var (location, domain) = (attempt.Request.Location, attempt.Request.Domain);
                return ok.Models
                    .Select(model => new DomainDataReceived(location, domain, model.Value, model.Key))
                    .Append(new DomainDataReceived(location, domain, ok.Forecast));
            })
            .Buffer(ForecastBufferSize, OverflowStrategy.DropHead)
            .WithAttributes(ActorAttributes.CreateSupervisionStrategy(StreamSupervision.LoggingDecider(_log)))
            .To(msg.SinkRef.Sink)
            .Run(Context.Materializer());
        _log.Info("Attached to the pipeline input");
    }
}
