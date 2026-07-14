using Akka;
using Akka.Actor;
using Akka.Streams;
using Akka.Streams.Dsl;
using Microsoft.Extensions.Options;
using Njord.Configuration;
using Njord.Domain.Analysis;
using Njord.Domain.Weather;
using Njord.Egress;
using Njord.Ingest;
using Njord.Pipeline;
using Servus.Akka;

namespace Njord.Mqtt;

public sealed class MqttPublisherActor : ReceiveActor, IWithStash
{
    private readonly string _baseTopic;
    private readonly IReadOnlyList<int> _horizons;
    private readonly int _forecastDays;
    private readonly ResolvedParameterSet _parameters;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<MqttPublisherActor> _logger;

    private ISinkRef<MqttMessage>? _mqttSinkRef;
    private ISourceRef<FetchOutcome>? _sourceRef;
    private IMaterializer? _mat;
    private ISourceQueueWithComplete<MqttMessage>? _queue;
    private Sink<MqttMessage, NotUsed>? _mergeHubSink;

    private readonly Dictionary<string, string> _lastPublished = new();
    private readonly Dictionary<(string Location, string ModelId, string Horizon), string> _lastPublishedHorizon = new();

    public IStash Stash { get; set; } = null!;

    public MqttPublisherActor(
        IOptions<NjordOptions> options,
        ResolvedParameterSet parameters,
        TimeProvider timeProvider,
        ILogger<MqttPublisherActor> logger)
    {
        var opts = options.Value;
        _baseTopic = opts.Mqtt.BaseTopic;
        _horizons = [.. opts.Horizons];
        _forecastDays = opts.ForecastDays;
        _parameters = parameters;
        _timeProvider = timeProvider;
        _logger = logger;

        WaitingForRefs();
    }

    protected override void PreStart()
    {
        _mat = Context.Materializer();

        var egressActor = Context.GetActor<EgressActor>();
        Context.Watch(egressActor);
        egressActor.Tell(new RegisterPublisher(Self));

        var connectionActor = Context.GetActor<MqttConnectionActor>();
        Context.Watch(connectionActor);
        connectionActor.Tell(new RequestMqttSink());

        var pipelineActor = Context.GetActor<PipelineActor>();
        Context.Watch(pipelineActor);
        pipelineActor.Tell(new RequestPipelineSource());
    }

    private void WaitingForRefs()
    {
        Receive<MqttSinkResponse>(response =>
        {
            _mqttSinkRef = response.SinkRef;
            _logger.LogInformation("MQTT SinkRef received");
            TryTransitionToReady();
        });
        Receive<PipelineSourceResponse>(response =>
        {
            _sourceRef = response.SourceRef;
            _logger.LogInformation("Pipeline SourceRef received");
            TryTransitionToReady();
        });
        Receive<Terminated>(msg => HandleTerminated(msg));
        ReceiveAny(_ => Stash.Stash());
    }

    private void TryTransitionToReady()
    {
        if (_mqttSinkRef is null || _sourceRef is null) return;

        MaterializePublisherGraph();
        MaterializeConsumerGraph(_sourceRef);
        _logger.LogInformation("Publisher pipeline materialized — ready");
        Become(Ready);
        Stash.UnstashAll();
    }

    private void Ready()
    {
        Receive<PublishStateResult>(msg =>
        {
            var messages = msg.Result switch
            {
                ConsensusResult r => StatePayloadBuilder.FromConsensus(r, _baseTopic, msg.Location),
                AlertResult r => StatePayloadBuilder.FromAlerts(r, _baseTopic),
                DerivedResult r => StatePayloadBuilder.FromDerived(r, _baseTopic),
                TrendResult r => StatePayloadBuilder.FromTrends(r, _baseTopic),
                IndexResult r => StatePayloadBuilder.FromIndices(r, _baseTopic),
                EnergyResult r => StatePayloadBuilder.FromEnergy(r, _baseTopic),
                HistoryResult r => StatePayloadBuilder.FromHistory(r, _baseTopic),
                _ => [],
            };

            foreach (var m in messages)
            {
                if (_lastPublished.TryGetValue(m.Topic, out var cached) && cached == m.Payload)
                    continue;
                _lastPublished[m.Topic] = m.Payload;
                _queue?.OfferAsync(m);
            }
        });
        Receive<Terminated>(msg => HandleTerminated(msg));
    }

    private void HandleTerminated(Terminated msg)
    {
        _logger.LogWarning("Watched actor {Actor} terminated — re-requesting refs",
            msg.ActorRef.Path.Name);

        _mqttSinkRef = null;
        _sourceRef = null;

        var egressActor = Context.GetActor<EgressActor>();
        Context.Watch(egressActor);
        egressActor.Tell(new RegisterPublisher(Self));

        var connectionActor = Context.GetActor<MqttConnectionActor>();
        Context.Watch(connectionActor);
        connectionActor.Tell(new RequestMqttSink());

        var pipelineActor = Context.GetActor<PipelineActor>();
        Context.Watch(pipelineActor);
        pipelineActor.Tell(new RequestPipelineSource());

        Become(WaitingForRefs);
    }

    private void MaterializePublisherGraph()
    {
        var mat = _mat!;

        var (queue, queueSource) = Source.Queue<MqttMessage>(32, OverflowStrategy.DropHead)
            .PreMaterialize(mat);
        _queue = queue;

        queueSource.RunWith(_mqttSinkRef!.Sink, mat);
    }

    private void MaterializeConsumerGraph(ISourceRef<FetchOutcome> sourceRef)
    {
        var mat = _mat!;
        var baseTopic = _baseTopic;
        var parameters = _parameters;
        var horizons = _horizons;
        var forecastDays = _forecastDays;
        var timeProvider = _timeProvider;
        var lastPublishedHorizon = _lastPublishedHorizon;

        sourceRef.Source
            .Collect(outcome => outcome is FetchOutcome.Success, outcome => (FetchOutcome.Success)outcome)
            .SelectMany(success =>
            {
                var forecast = success.Forecast;
                var perHorizon = StatePayloadBuilder.BuildPerHorizon(
                    forecast, parameters, horizons, forecastDays, timeProvider.GetUtcNow());
                var messages = new List<MqttMessage>();

                foreach (var (horizon, payload) in perHorizon)
                {
                    var key = (forecast.Location, forecast.Model.Id, horizon);
                    if (lastPublishedHorizon.TryGetValue(key, out var cached) && cached == payload)
                        continue;

                    lastPublishedHorizon[key] = payload;
                    var topic = TopicScheme.HorizonTopic(baseTopic, forecast.Location, forecast.Model, horizon);
                    messages.Add(new MqttMessage(topic, payload, true));
                }

                return messages;
            })
            .WithAttributes(ActorAttributes.CreateSupervisionStrategy(
                _ => Akka.Streams.Supervision.Directive.Resume))
            .RunWith(_mqttSinkRef!.Sink, mat);
    }
}
