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
    private readonly IOpenMeteoClient _client;
    private readonly TimeProvider _timeProvider;
    private readonly NjordOptions _options;
    private readonly int _weight;
    private readonly IGatewayBudgetGate _gate;
    private readonly Dictionary<string, IActorRef> _pendingRequests = new();
    private readonly ILoggingAdapter _log = Context.GetLogger();
    private IMaterializer? _mat;
    private ISourceQueueWithComplete<FetchTarget>? _fetchQueue;
    private ISourceQueueWithComplete<WeatherForecast>? _forecastQueue;
    private Source<WeatherForecast, NotUsed>? _broadcastSource;

    internal sealed record FetchTarget(LocationOptions Location, WeatherModel Model, CycleId Cycle, int Weight);
    private sealed record FetchResult(PollOutcome Outcome, string Key);
    private sealed record StreamCompleted;

    public OpenMeteoGateway(
        IOpenMeteoClient client,
        TimeProvider timeProvider,
        IOptions<NjordOptions> options,
        ResolvedParameterSet parameters,
        IOptionsMonitor<NjordOptions> optionsMonitor)
    {
        _client = client;
        _timeProvider = timeProvider;
        _options = options.Value;
        _weight = ComputeWeight(parameters.HourlyCount, _options.ForecastDays);

        var budgetTracker = Context.GetActor<IBudgetTracker>();
        var provider = new OptionsBudgetProvider(optionsMonitor);
        _gate = new GatewayBudgetGate(provider, budgetTracker, timeProvider);

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
        Receive<StreamCompleted>(_ => _log.Warning("Gateway fetch stream completed unexpectedly"));
    }

    private void OnFetchForecast(FetchForecast msg)
    {
        var key = $"{msg.Location}|{msg.ModelId}";
        _pendingRequests[key] = Sender;

        var location = _options.FindLocation(msg.Location);
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
        if (!_pendingRequests.Remove(msg.Key, out var requester))
            return;

        switch (msg.Outcome)
        {
            case PollOutcome.Success s:
                var hash = ForecastDataHash.Compute(s.Forecast, _timeProvider);
                requester.Tell(new FetchForecastCompleted(s.Forecast.Location, s.Forecast.Model.Id, hash));
                _forecastQueue?.OfferAsync(s.Forecast);
                break;
            case PollOutcome.Failure f:
                requester.Tell(new FetchForecastFailed(f.Location, f.Model.Id, f.Reason, f.Detail));
                break;
        }
    }

    private void OnRequestForecastStream(RequestForecastStream msg)
    {
        if (_broadcastSource is null)
        {
            Sender.Tell(new ForecastStreamFailed(msg.RequestId,
                new InvalidOperationException("Gateway stream not materialized")));
            return;
        }

        _broadcastSource
            .RunWith(StreamRefs.SourceRef<WeatherForecast>(), _mat!)
            .PipeTo(Sender, Self,
                sr => new RequestForecastStreamCompleted(msg.RequestId, sr),
                ex => new ForecastStreamFailed(msg.RequestId, ex));
    }

    private void MaterializeStreams()
    {
        var (fetchQueue, fetchQueueSource) = Source.Queue<FetchTarget>(16, OverflowStrategy.Backpressure)
            .PreMaterialize(_mat);

        fetchQueueSource
            .Via(new GatewayThrottleStage(_gate))
            .SelectAsyncUnordered(2, async target =>
            {
                var outcome = await _client.FetchAsync(target.Location, target.Model, target.Cycle, CancellationToken.None);
                var key = $"{target.Location.Name}|{target.Model.Id}";
                return new FetchResult(outcome, key);
            })
            .WithAttributes(ActorAttributes.CreateSupervisionStrategy(StreamSupervision.LoggingDecider(_log)))
            .To(Sink.ActorRef<FetchResult>(Self, new StreamCompleted(), ex => new StreamCompleted()))
            .Run(_mat);

        var (forecastQueue, forecastQueueSource) = Source.Queue<WeatherForecast>(16, OverflowStrategy.DropHead)
            .PreMaterialize(_mat);

        var (broadcastSource, broadcastSink) = BroadcastHub.Sink<WeatherForecast>(bufferSize: 2)
            .PreMaterialize(_mat);

        forecastQueueSource.To(broadcastSink).Run(_mat);

        _fetchQueue = fetchQueue;
        _forecastQueue = forecastQueue;
        _broadcastSource = broadcastSource;
    }

    internal static int ComputeWeight(int hourlyVariableCount, int forecastDays) =>
        (int)Math.Ceiling(hourlyVariableCount / 10.0) * (int)Math.Ceiling(forecastDays / 14.0);
}
