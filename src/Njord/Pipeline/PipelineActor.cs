using Akka;
using Akka.Actor;
using Akka.Hosting;
using Akka.Streams;
using Akka.Streams.Dsl;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Njord.Configuration;
using Njord.Domain;
using Njord.Egress;
using Njord.Ingest;

namespace Njord.Pipeline;

public sealed class PipelineActor : ReceiveActor, IWithStash
{
    private readonly NjordOptions _options;
    private readonly ResolvedParameterSet _parameters;
    private readonly IOpenMeteoClient _client;
    private readonly TimeProvider _timeProvider;
    private readonly ILoggerFactory _loggerFactory;
    private readonly ActorRegistry _registry;
    private IActorRef _egressActor = ActorRefs.Nobody;
    private ISourceQueueWithComplete<WeightedTarget>? _queue;

    public IStash Stash { get; set; } = null!;

    public PipelineActor(
        IOptions<NjordOptions> options,
        ResolvedParameterSet parameters,
        IOpenMeteoClient client,
        TimeProvider timeProvider,
        ILoggerFactory loggerFactory,
        ActorRegistry registry)
    {
        _options = options.Value;
        _parameters = parameters;
        _client = client;
        _timeProvider = timeProvider;
        _loggerFactory = loggerFactory;
        _registry = registry;

        WaitingForSinkRef();
    }

    protected override void PreStart()
    {
        _egressActor = _registry.Get<MqttEgressActor>();
        Context.Watch(_egressActor);
        _egressActor.Tell(new RequestEgressSink());
    }

    private void WaitingForSinkRef()
    {
        Receive<EgressSinkResponse>(response =>
        {
            MaterializePipeline(response.SinkRef);
            Become(Running);
            Stash.UnstashAll();
        });
        Receive<Terminated>(_ => RequestNewSinkRef());
        ReceiveAny(_ => Stash.Stash());
    }

    private void Running()
    {
        Receive<RequestPipelineQueue>(_ =>
        {
            if (_queue is not null)
                Sender.Tell(new PipelineQueueResponse(_queue));
        });
        Receive<Terminated>(_ =>
        {
            _logger.LogWarning("Egress actor terminated — waiting for new SinkRef");
            _queue = null;
            RequestNewSinkRef();
            Become(WaitingForSinkRef);
        });
    }

    private ILogger _logger => _loggerFactory.CreateLogger<PipelineActor>();

    private void RequestNewSinkRef()
    {
        _egressActor = _registry.Get<MqttEgressActor>();
        Context.Watch(_egressActor);
        _egressActor.Tell(new RequestEgressSink());
    }

    private void MaterializePipeline(ISinkRef<MqttMessage> sinkRef)
    {
        var mat = Context.Materializer();
        var budget = _options.EffectiveBudget;
        var budgetPerMinute = (int)(budget.RequestsPerMinute * 0.8);
        var baseTopic = _options.Mqtt.BaseTopic;
        var horizons = (IReadOnlyList<int>)[.. _options.Horizons];
        var forecastDays = _options.ForecastDays;
        var lastPublished = new System.Collections.Concurrent.ConcurrentDictionary<(string, string, string), string>();
        var schedulerActor = _registry.Get<SchedulerActor>();

        // Side-effect: publish messages to egress via SinkRef
        var egressSink = sinkRef.Sink;
        var egressMat = Source.Queue<MqttMessage>(128, OverflowStrategy.DropHead)
            .To(egressSink)
            .Run(mat);

        _queue = Source.Queue<WeightedTarget>(64, OverflowStrategy.Backpressure)
            .Throttle(budgetPerMinute, TimeSpan.FromMinutes(1), budgetPerMinute,
                element => element.Weight, ThrottleMode.Shaping)
            .Via(FetchStage.Create(_client, _timeProvider))
            .Collect(outcome => outcome is FetchOutcome.Success s ? s : null!)
            .Where(s => s is not null)
            .Select(success =>
            {
                var forecast = success.Forecast;
                var perHorizon = StatePayloadBuilder.BuildPerHorizon(forecast, _parameters, horizons, forecastDays);

                foreach (var (horizon, payload) in perHorizon)
                {
                    var key = (forecast.Location, forecast.Model.Id, horizon);
                    if (lastPublished.TryGetValue(key, out var cached) && cached == payload)
                        continue;

                    lastPublished[key] = payload;
                    var topic = TopicScheme.HorizonTopic(baseTopic, forecast.Location, forecast.Model, horizon);
                    egressMat.OfferAsync(new MqttMessage(topic, payload, true));
                }

                return success;
            })
            .Select(success => new HashResult(
                success.Forecast.Location,
                success.Forecast.Model.Id,
                ForecastDataHash.Compute(success.Forecast, _timeProvider)))
            .Ask<Ack>(schedulerActor, TimeSpan.FromSeconds(5))
            .To(Sink.Ignore<Ack>())
            .Run(mat);
    }
}
