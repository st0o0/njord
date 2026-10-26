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

// The actor is lifecycle only: it resolves the poll region and the pipeline, attaches to the pipeline input once and
// feeds requests into a stream. The stream does all the work (budget gate, client call) and sends the replies
// (Completed, Failed, Rejected) straight to the poll shard region, which routes them to the requesting worker.
// A failed or lost attach, or a failed stream, crashes the actor and supervision restarts it.
public sealed class OpenMeteoGateway : ReceiveActor
{
    private const int ForecastBranchSize = 16;
    private const int RequestQueueSize = 64;

    // Generations are unique across incarnations: a restarted gateway keeps its ActorRef but gets a fresh instance,
    // so stream signals of the previous incarnation must never match the current one.
    private static long LastGeneration;

    private sealed record Resolved(IActorRef PollRegion, IActorRef Pipeline);

    private sealed record AttachFailed(string Reason, Exception? Cause);

    private sealed record Offered(FetchDomain Request, IQueueOfferResult Result);

    private sealed record StreamFailed(long Generation, Exception Cause);

    private sealed record StreamCompleted(long Generation);

    private sealed record GatedRequest(FetchDomain Request, IDomainClient? Client, FetchRejectionReason? Rejected);

    private sealed record FetchAttempt(
        FetchDomain Request,
        FetchResult? Result,
        FetchRejectionReason? Rejected,
        Exception? Error);

    private readonly IReadOnlyDictionary<DomainKind, IDomainClient> _clients;
    private readonly TimeProvider _timeProvider;
    private readonly FetchTimeouts _timeouts;
    private readonly IGatewayBudgetGate _gate;
    private readonly ILoggingAdapter _log = Context.GetLogger();
    private readonly IMaterializer _materializer = Context.Materializer();
    private readonly long _generation = Interlocked.Increment(ref LastGeneration);

    private IActorRef? _pollRegion;
    private ISourceQueueWithComplete<FetchDomain>? _requestQueue;

    public OpenMeteoGateway(
        IReadOnlyDictionary<DomainKind, IDomainClient> clients,
        TimeProvider timeProvider,
        IBudgetProvider budgetProvider,
        FetchTimeouts timeouts)
    {
        _clients = clients;
        _timeProvider = timeProvider;
        _timeouts = timeouts;
        _gate = new GatewayBudgetGate(budgetProvider, Context.GetActor<IBudgetTracker>(), timeProvider);

        Receive<FetchDomain>(OnFetchDomain);
        Receive<Offered>(OnOffered);
        Receive<StreamFailed>(msg => msg.Generation == _generation,
            msg => throw new InvalidOperationException("The fetch stream failed", msg.Cause));
        Receive<StreamCompleted>(msg => msg.Generation == _generation,
            _ => throw new InvalidOperationException("The fetch stream completed unexpectedly"));
        Receive<StreamFailed>(_ => { });
        Receive<StreamCompleted>(_ => { });

        Receive<Resolved>(OnResolved);
        Receive<RequestForecastSinkCompleted>(OnSinkCompleted);
        Receive<RequestForecastSinkFailed>(msg =>
            throw new InvalidOperationException("The pipeline could not create a SinkRef", msg.Cause));
        Receive<AttachFailed>(msg =>
            throw new InvalidOperationException($"Not attached to the pipeline input: {msg.Reason}", msg.Cause));
        Receive<Terminated>(_ => throw new InvalidOperationException("The pipeline terminated"));
    }

    protected override void PreStart()
    {
        var region = Context.GetActorAsync<IPollRegion>();
        var pipeline = Context.GetActorAsync<IForecastPipeline>();
        ResolveAsync(region, pipeline).PipeTo(Self,
            success: resolved => resolved,
            failure: ex => new AttachFailed("the poll region or the pipeline could not be resolved", ex));
    }

    protected override void PostStop()
    {
        _requestQueue?.Complete();
    }

    private static async Task<Resolved> ResolveAsync(Task<IActorRef> region, Task<IActorRef> pipeline) =>
        new(await region, await pipeline);

    private void OnFetchDomain(FetchDomain msg)
    {
        if (_requestQueue is not { } queue)
        {
            _pollRegion?.Tell(new FetchDomainRejected(msg.Location, msg.Domain,
                FetchRejectionReason.PipelineUnavailable));
            return;
        }

        queue.OfferAsync(msg).PipeTo(Self,
            success: result => new Offered(msg, result),
            failure: ex => new Offered(msg, new QueueOfferResult.Failure(ex)));
    }

    // Only requests the queue refused need an answer here; accepted ones are answered by the stream.
    private void OnOffered(Offered msg)
    {
        if (msg.Result is QueueOfferResult.Enqueued)
        {
            return;
        }

        var reason = msg.Result is QueueOfferResult.Dropped
            ? FetchRejectionReason.QueueFull
            : FetchRejectionReason.PipelineUnavailable;
        _pollRegion?.Tell(new FetchDomainRejected(msg.Request.Location, msg.Request.Domain, reason));
    }

    private void OnResolved(Resolved msg)
    {
        _pollRegion = msg.PollRegion;
        Context.Watch(msg.Pipeline);
        msg.Pipeline.Ask<RequestForecastSinkResponse>(new RequestForecastSink(0), _timeouts.Admission).PipeTo(Self,
            success: response => response,
            failure: ex => new AttachFailed("the pipeline did not answer the SinkRef request", ex));
    }

    private void OnSinkCompleted(RequestForecastSinkCompleted msg)
    {
        var region = _pollRegion!;
        var clients = _clients;
        var gate = _gate;
        var time = _timeProvider;

        var pipelineSink = Flow.Create<FetchAttempt>()
            .SelectMany(a => a.Result is FetchSuccess ok
                ? ok.Models
                    .Select(m => new DomainDataReceived(a.Request.Location, a.Request.Domain, m.Value, m.Key))
                    .Append(new DomainDataReceived(a.Request.Location, a.Request.Domain, ok.Forecast))
                : [])
            .Buffer(ForecastBranchSize, OverflowStrategy.DropHead)
            .To(msg.SinkRef.Sink);

        var replySink = Sink.ForEach<FetchDomainResponse>(reply => region.Tell(reply));

        var (queue, done) = Source.Queue<FetchDomain>(RequestQueueSize, OverflowStrategy.DropNew)
            .Select(request =>
            {
                if (!clients.TryGetValue(request.Domain, out var client))
                    return new GatedRequest(request, null, FetchRejectionReason.PipelineUnavailable);
                if (!gate.TryAcquire(client.CallWeight))
                    return new GatedRequest(request, null, FetchRejectionReason.Budget);
                return Task.FromResult(new GatedRequest(request, client, null));
            })
            .SelectAsyncUnordered(3, async gated =>
            {
                if (gated.Rejected is { } rejection)
                {
                    RecordTelemetry(gated.Request, "rejected", TimeSpan.Zero);
                    return new FetchAttempt(gated.Request, null, rejection, null);
                }

                var start = time.GetTimestamp();
                try
                {
                    var result = await gated.Client!.FetchAsync(
                        gated.Request.Location.Latitude, gated.Request.Location.Longitude, time.GetUtcNow(),
                        CancellationToken.None);
                    RecordTelemetry(gated.Request, result is FetchSuccess ? "success" : "failure",
                        time.GetElapsedTime(start));
                    return new FetchAttempt(gated.Request, result, null, null);
                }
                catch (Exception ex)
                {
                    RecordTelemetry(gated.Request, "failure", time.GetElapsedTime(start));
                    return new FetchAttempt(gated.Request, null, null, ex);
                }
            })
            .AlsoToMaterialized(pipelineSink, Keep.Left)
            .Select(attempt =>
            {
                var (location, domain) = (attempt.Request.Location, attempt.Request.Domain);
                return (FetchDomainResponse)(attempt switch
                {
                    { Rejected: { } rejection } => new FetchDomainRejected(location, domain, rejection),
                    { Error: { } error } => new FetchDomainFailed(location, domain, PollFailureReason.Transport,
                        $"Fetch threw {error.GetType().Name}: {error.Message}"),
                    { Result: FetchSuccess ok } =>
                        new FetchDomainCompleted(location, domain, ok.Forecast.GetHashCode()),
                    { Result: FetchError failure } => new FetchDomainFailed(location, domain, failure.Reason,
                        failure.Message),
                    _ => new FetchDomainFailed(location, domain, PollFailureReason.MalformedPayload,
                        $"The {domain.ToTelemetryTag()} endpoint returned no usable data"),
                });
            })
            .ToMaterialized(replySink, Keep.Both)
            .Run(_materializer);

        _requestQueue = queue;
        done.PipeTo(Self,
            success: () => new StreamCompleted(_generation),
            failure: ex => new StreamFailed(_generation, ex));
        _log.Info("Attached to the pipeline input");
    }

    private static void RecordTelemetry(FetchDomain request, string outcome, TimeSpan elapsed)
    {
        var location = new KeyValuePair<string, object?>("location", request.Location.Name);
        var domain = new KeyValuePair<string, object?>("domain", request.Domain.ToTelemetryTag());
        Telemetry.FetchTotal.Add(1, location, domain, new KeyValuePair<string, object?>("outcome", outcome));
        if (outcome == "success")
            Telemetry.FetchDuration.Record(elapsed.TotalSeconds, location, domain);
    }
}
