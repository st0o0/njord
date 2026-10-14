using Akka;
using Akka.Actor;
using Akka.Event;
using Akka.Streams;
using Akka.Streams.Dsl;
using Microsoft.Extensions.Options;
using Njord.Core.Actors;
using Njord.Core.Configuration;
using Njord.Messages.Forecast;
using Njord.Weather;
using Servus.Akka;
using WeatherForecast = Njord.Weather.Forecast;

namespace Njord.OpenMeteo;

public sealed class OpenMeteoGateway : ReceiveActor
{
    // Requests that do not fit the fetch queue yet wait here; beyond this bound they are answered at once.
    internal const int MaxWaitingRequests = 1024;

    internal sealed record FetchTarget(long RequestId, LocationOptions Location, WeatherModel Model, CycleId Cycle, int Weight);
    private sealed record FetchResult(PollOutcome Outcome, long RequestId);
    private sealed record FetchStreamStopped(int Generation);
    private sealed record OfferCompleted(long RequestId, int Generation, IQueueOfferResult Result);
    private sealed record OfferFailed(long RequestId, int Generation, Exception Cause);

    private readonly IOpenMeteoClient _client;
    private readonly TimeProvider _timeProvider;
    private readonly NjordOptions _options;
    private readonly int _weight;
    private readonly IGatewayBudgetGate _gate;

    // Location and model as the requester spelled them: replies echo them so the requester can correlate.
    private sealed record PendingRequest(IActorRef Requester, LocationId Location, string ModelId, FetchTarget Target)
    {
        public bool Offered { get; set; }
    }

    private readonly Dictionary<long, PendingRequest> _pendingRequests = new();
    private readonly Queue<long> _waiting = new();
    private long _nextRequestId;
    private long? _offering;
    private IMaterializer? _mat;
    private int _fetchStreamGeneration;
    private ISourceQueueWithComplete<FetchTarget>? _fetchQueue;
    private ISourceQueueWithComplete<WeatherForecast>? _forecastQueue;
    private Source<WeatherForecast, NotUsed>? _broadcastSource;
    private readonly ILoggingAdapter _log = Context.GetLogger();

    public OpenMeteoGateway(
        IOpenMeteoClient client,
        TimeProvider timeProvider,
        IOptions<NjordOptions> options,
        ResolvedParameterSet parameters,
        IBudgetProvider budgetProvider)
    {
        _client = client;
        _timeProvider = timeProvider;
        _options = options.Value;
        _weight = ComputeWeight(parameters.HourlyCount, _options.ForecastDays);

        var budgetTracker = Context.GetActor<IBudgetTracker>();
        _gate = new GatewayBudgetGate(budgetProvider, budgetTracker, timeProvider);

        Ready();
    }

    protected override void PreStart()
    {
        _mat = Context.Materializer();
        MaterializeStreams();
        _log.Info("OpenMeteoGateway stream materialized");
    }

    private void Ready()
    {
        Receive<FetchForecast>(OnFetchForecast);
        Receive<OfferCompleted>(OnOfferCompleted);
        Receive<OfferFailed>(OnOfferFailed);
        Receive<FetchResult>(OnFetchResult);
        Receive<RequestForecastStream>(OnRequestForecastStream);
        Receive<FetchStreamStopped>(OnFetchStreamStopped);
    }

    private void OnFetchForecast(FetchForecast msg)
    {
        var location = _options.FindLocation(msg.Location.Name);
        if (location is null)
        {
            Sender.Tell(new FetchForecastFailed(msg.Location, msg.ModelId,
                PollFailureReason.ModelUnavailable, $"Location '{msg.Location}' not found in config"));
            return;
        }

        if (_waiting.Count >= MaxWaitingRequests)
        {
            _log.Warning("Gateway request backlog is full ({Count}) - rejecting {Location}/{Model}",
                _waiting.Count, msg.Location, msg.ModelId);
            Sender.Tell(new FetchForecastFailed(msg.Location, msg.ModelId,
                PollFailureReason.Transport, $"Gateway request backlog is full ({MaxWaitingRequests} waiting)"));
            return;
        }

        var id = ++_nextRequestId;
        var cycle = new CycleId(_timeProvider.GetUtcNow());
        var target = new FetchTarget(id, location, new WeatherModel(ResolveModel(location, msg.ModelId)), cycle, _weight);
        _pendingRequests[id] = new PendingRequest(Sender, msg.Location, msg.ModelId, target);
        _waiting.Enqueue(id);
        OfferNext();
    }

    // A backpressured queue accepts only one pending offer: further offers fault, so requests are
    // handed over one at a time and each offer result comes back as a message.
    private void OfferNext()
    {
        while (_offering is null && _fetchQueue is { } queue && _waiting.TryDequeue(out var id))
        {
            if (!_pendingRequests.TryGetValue(id, out var pending))
            {
                continue;
            }

            pending.Offered = true;
            _offering = id;
            var generation = _fetchStreamGeneration;
            Task<IQueueOfferResult> offer;
            try
            {
                offer = queue.OfferAsync(pending.Target);
            }
            catch (Exception ex)
            {
                offer = Task.FromException<IQueueOfferResult>(ex);
            }

            offer.PipeTo(Self,
                success: result => new OfferCompleted(id, generation, result),
                failure: ex => new OfferFailed(id, generation, ex));
        }
    }

    private void OnOfferCompleted(OfferCompleted msg)
    {
        if (!IsCurrentOffer(msg.RequestId, msg.Generation))
        {
            return;
        }

        if (msg.Result is not QueueOfferResult.Enqueued)
        {
            var detail = msg.Result is QueueOfferResult.Failure failure
                ? $"{failure.GetType().Name}: {failure.Cause.Message}"
                : msg.Result.GetType().Name;
            FailRequest(msg.RequestId, $"Gateway could not enqueue the request ({detail})");
        }

        OfferNext();
    }

    private void OnOfferFailed(OfferFailed msg)
    {
        if (!IsCurrentOffer(msg.RequestId, msg.Generation))
        {
            return;
        }

        FailRequest(msg.RequestId,
            $"Gateway could not enqueue the request ({msg.Cause.GetType().Name}: {msg.Cause.Message})");
        OfferNext();
    }

    private bool IsCurrentOffer(long requestId, int generation)
    {
        if (generation != _fetchStreamGeneration || _offering != requestId)
        {
            return false;
        }

        _offering = null;
        return true;
    }

    private void FailRequest(long requestId, string detail)
    {
        if (_pendingRequests.TryGetValue(requestId, out var pending))
        {
            _log.Warning("Fetch request for {Location}/{Model} failed: {Detail}", pending.Location, pending.ModelId, detail);
        }

        Reply(requestId, p => new FetchForecastFailed(p.Location, p.ModelId, PollFailureReason.Transport, detail));
    }

    // The only way a requester is answered: the request is forgotten first, so it is answered at most once.
    private void Reply(long requestId, Func<PendingRequest, FetchForecastResponse> reply)
    {
        if (_pendingRequests.Remove(requestId, out var pending))
        {
            pending.Requester.Tell(reply(pending));
        }
    }

    private string ResolveModel(LocationOptions location, string modelId) =>
        _options.Models.Concat(location.Models ?? [])
            .FirstOrDefault(m => m.Equals(modelId, StringComparison.OrdinalIgnoreCase)) ?? modelId;

    private void OnFetchResult(FetchResult msg)
    {
        switch (msg.Outcome)
        {
            case PollOutcome.Success s:
                var hash = ForecastDataHash.Compute(s.Forecast, _timeProvider);
                Reply(msg.RequestId, p => new FetchForecastCompleted(p.Location, p.ModelId, hash));
                _forecastQueue?.OfferAsync(s.Forecast);
                break;
            case PollOutcome.Failure f:
                Reply(msg.RequestId, p => new FetchForecastFailed(p.Location, p.ModelId, f.Reason, f.Detail));
                break;
        }
    }

    private void OnRequestForecastStream(RequestForecastStream msg)
    {
        if (_broadcastSource is null)
        {
            Sender.Tell(new RequestForecastStreamFailed(msg.RequestId,
                new InvalidOperationException("Gateway stream not materialized")));
            return;
        }

        _broadcastSource
            .RunWith(StreamRefs.SourceRef<WeatherForecast>(), _mat!)
            .PipeTo(Sender, Self,
                sr => new RequestForecastStreamCompleted(msg.RequestId, sr),
                ex => new RequestForecastStreamFailed(msg.RequestId, ex));
    }

    // A stopped fetch stream would leave every requester unanswered and drop later offers silently.
    // Re-materializing only the fetch stream keeps the forecast hub (and its SourceRefs) and the
    // budget gate; the requests the stopped stream held come back to the scheduler as ordinary
    // transport failures, requests still waiting are offered to the new stream.
    private void OnFetchStreamStopped(FetchStreamStopped msg)
    {
        if (msg.Generation != _fetchStreamGeneration)
        {
            return;
        }

        var lost = _pendingRequests.Where(p => p.Value.Offered).Select(p => p.Key).ToList();
        _log.Warning("Gateway fetch stream stopped - failing {Count} in-flight requests and re-materializing",
            lost.Count);

        foreach (var id in lost)
        {
            Reply(id, p => new FetchForecastFailed(p.Location, p.ModelId,
                PollFailureReason.Transport, "Gateway fetch stream stopped"));
        }

        _offering = null;
        MaterializeFetchStream();
        OfferNext();
    }

    private void MaterializeStreams()
    {
        MaterializeFetchStream();

        var (forecastQueue, forecastQueueSource) = Source.Queue<WeatherForecast>(16, OverflowStrategy.DropHead)
            .PreMaterialize(_mat);

        var (broadcastSource, broadcastSink) = BroadcastHub.Sink<WeatherForecast>(bufferSize: 2)
            .PreMaterialize(_mat);

        forecastQueueSource.To(broadcastSink).Run(_mat);

        _forecastQueue = forecastQueue;
        _broadcastSource = broadcastSource;
    }

    private void MaterializeFetchStream()
    {
        var generation = ++_fetchStreamGeneration;
        var (fetchQueue, fetchQueueSource) = Source.Queue<FetchTarget>(16, OverflowStrategy.Backpressure)
            .PreMaterialize(_mat);

        fetchQueueSource
            .Via(new GatewayThrottleStage(_gate))
            .SelectAsyncUnordered(2, FetchAsync)
            .WithAttributes(ActorAttributes.CreateSupervisionStrategy(StreamSupervision.LoggingDecider(_log)))
            .To(Sink.ActorRef<FetchResult>(Self, new FetchStreamStopped(generation), _ => new FetchStreamStopped(generation)))
            .Run(_mat);

        _fetchQueue = fetchQueue;
    }

    // A client that throws instead of returning a PollOutcome still has to answer its requester,
    // otherwise the scheduler never re-schedules that target.
    private async Task<FetchResult> FetchAsync(FetchTarget target)
    {
        try
        {
            var outcome = await _client.FetchAsync(target.Location, target.Model, target.Cycle, CancellationToken.None);
            return new FetchResult(outcome, target.RequestId);
        }
        catch (Exception ex)
        {
            _log.Warning(ex, "Fetch for {Location}/{Model} threw {ExceptionType}", target.Location.Name, target.Model.Id, ex.GetType().Name);
            var failure = new PollOutcome.Failure(target.Location.Name, target.Model, PollFailureReason.Transport,
                $"Fetch threw {ex.GetType().Name}: {ex.Message}");
            return new FetchResult(failure, target.RequestId);
        }
    }

    internal static int ComputeWeight(int hourlyVariableCount, int forecastDays) =>
        (int)Math.Ceiling(hourlyVariableCount / 10.0) * (int)Math.Ceiling(forecastDays / 14.0);
}
