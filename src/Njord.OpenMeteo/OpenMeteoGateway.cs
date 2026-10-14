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
    internal sealed record FetchTarget(LocationOptions Location, WeatherModel Model, CycleId Cycle, int Weight);
    private sealed record FetchResult(PollOutcome Outcome, string Key);
    private sealed record FetchStreamStopped(int Generation);

    private readonly IOpenMeteoClient _client;
    private readonly TimeProvider _timeProvider;
    private readonly NjordOptions _options;
    private readonly int _weight;
    private readonly IGatewayBudgetGate _gate;

    private sealed record PendingRequest(IActorRef Requester, LocationId Location, string ModelId);

    private readonly Dictionary<string, PendingRequest> _pendingRequests = new();
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
        Receive<FetchResult>(OnFetchResult);
        Receive<RequestForecastStream>(OnRequestForecastStream);
        Receive<FetchStreamStopped>(OnFetchStreamStopped);
    }

    private void OnFetchForecast(FetchForecast msg)
    {
        var key = $"{msg.Location}|{msg.ModelId}";
        _pendingRequests[key] = new PendingRequest(Sender, msg.Location, msg.ModelId);

        var location = _options.FindLocation(msg.Location.Name);
        if (location is null)
        {
            Sender.Tell(new FetchForecastFailed(msg.Location, msg.ModelId,
                PollFailureReason.ModelUnavailable, $"Location '{msg.Location}' not found in config"));
            _pendingRequests.Remove(key);
            return;
        }

        var cycle = new CycleId(_timeProvider.GetUtcNow());
        var target = new FetchTarget(location, new WeatherModel(msg.ModelId), cycle, _weight);
        _fetchQueue?.OfferAsync(target);
    }

    private void OnFetchResult(FetchResult msg)
    {
        if (!_pendingRequests.Remove(msg.Key, out var pending))
            return;

        var requester = pending.Requester;

        switch (msg.Outcome)
        {
            case PollOutcome.Success s:
                var hash = ForecastDataHash.Compute(s.Forecast, _timeProvider);
                requester.Tell(new FetchForecastCompleted(new LocationId(s.Forecast.Location), s.Forecast.Model.Id, hash));
                _forecastQueue?.OfferAsync(s.Forecast);
                break;
            case PollOutcome.Failure f:
                requester.Tell(new FetchForecastFailed(new LocationId(f.Location), f.Model.Id, f.Reason, f.Detail));
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
    // budget gate; the lost requests come back to the scheduler as ordinary transport failures.
    private void OnFetchStreamStopped(FetchStreamStopped msg)
    {
        if (msg.Generation != _fetchStreamGeneration)
        {
            return;
        }

        _log.Warning("Gateway fetch stream stopped - failing {Count} in-flight requests and re-materializing",
            _pendingRequests.Count);

        foreach (var pending in _pendingRequests.Values)
        {
            pending.Requester.Tell(new FetchForecastFailed(pending.Location, pending.ModelId,
                PollFailureReason.Transport, "Gateway fetch stream stopped"));
        }

        _pendingRequests.Clear();
        MaterializeFetchStream();
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
        var key = $"{target.Location.Name}|{target.Model.Id}";
        try
        {
            var outcome = await _client.FetchAsync(target.Location, target.Model, target.Cycle, CancellationToken.None);
            return new FetchResult(outcome, key);
        }
        catch (Exception ex)
        {
            _log.Warning(ex, "Fetch for {Location}/{Model} threw {ExceptionType}", target.Location.Name, target.Model.Id, ex.GetType().Name);
            var failure = new PollOutcome.Failure(target.Location.Name, target.Model, PollFailureReason.Transport,
                $"Fetch threw {ex.GetType().Name}: {ex.Message}");
            return new FetchResult(failure, key);
        }
    }

    internal static int ComputeWeight(int hourlyVariableCount, int forecastDays) =>
        (int)Math.Ceiling(hourlyVariableCount / 10.0) * (int)Math.Ceiling(forecastDays / 14.0);
}
