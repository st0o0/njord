using Akka.Actor;
using Akka.Streams;
using Akka.Streams.Dsl;
using Microsoft.Extensions.Options;
using Njord.Configuration;
using Njord.Domain.Weather;
using Njord.Egress;
using Njord.Enrichment;
using Servus.Akka;

namespace Njord.Mqtt;

public sealed class MqttEgressActor : ReceiveActor, IWithStash
{
    private readonly string _baseTopic;
    private readonly ResolvedParameterSet _parameters;
    private readonly IReadOnlyList<int> _horizons;
    private readonly int _forecastDays;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<MqttEgressActor> _logger;
    private readonly Dictionary<string, IEnrichmentFeature> _featuresByType;

    private ISinkRef<MqttMessage>? _mqttSinkRef;
    private ISourceRef<EgressEvent>? _egressSourceRef;
    private IMaterializer? _mat;

    public IStash Stash { get; set; } = null!;

    public MqttEgressActor(
        IOptions<NjordOptions> options,
        ResolvedParameterSet parameters,
        TimeProvider timeProvider,
        IEnumerable<IEnrichmentFeature> features,
        ILogger<MqttEgressActor> logger)
    {
        var opts = options.Value;
        _baseTopic = opts.Mqtt.BaseTopic;
        _parameters = parameters;
        _horizons = [.. opts.Horizons];
        _forecastDays = opts.ForecastDays;
        _timeProvider = timeProvider;
        _logger = logger;
        _featuresByType = features.ToDictionary(f => f.TypeName);

        WaitingForRefs();
    }

    protected override void PreStart()
    {
        _mat = Context.Materializer();

        var egressActor = Context.GetActor<EgressActor>();
        Context.Watch(egressActor);
        egressActor.Tell(new RequestEgressSource());

        var connectionActor = Context.GetActor<MqttConnectionActor>();
        Context.Watch(connectionActor);
        connectionActor.Tell(new RequestMqttSink());
    }

    private void WaitingForRefs()
    {
        Receive<EgressSourceResponse>(response =>
        {
            _egressSourceRef = response.SourceRef;
            _logger.LogInformation("Egress SourceRef received");
            TryTransitionToReady();
        });
        Receive<MqttSinkResponse>(response =>
        {
            _mqttSinkRef = response.SinkRef;
            _logger.LogInformation("MQTT SinkRef received");
            TryTransitionToReady();
        });
        Receive<Terminated>(HandleTerminated);
        ReceiveAny(_ => Stash.Stash());
    }

    private void TryTransitionToReady()
    {
        if (_egressSourceRef is null || _mqttSinkRef is null)
        {
            return;
        }

        MaterializeGraph();
        _logger.LogInformation("MQTT egress pipeline materialized — ready");
        Become(Ready);
        Stash.UnstashAll();
    }

    private void Ready()
    {
        Receive<Terminated>(HandleTerminated);
    }

    private void HandleTerminated(Terminated msg)
    {
        _logger.LogWarning("Watched actor {Actor} terminated — re-requesting refs", msg.ActorRef.Path.Name);

        _mqttSinkRef = null;
        _egressSourceRef = null;

        var egressActor = Context.GetActor<EgressActor>();
        Context.Watch(egressActor);
        egressActor.Tell(new RequestEgressSource());

        var connectionActor = Context.GetActor<MqttConnectionActor>();
        Context.Watch(connectionActor);
        connectionActor.Tell(new RequestMqttSink());

        Become(WaitingForRefs);
    }

    private void MaterializeGraph()
    {
        var mat = _mat!;
        var baseTopic = _baseTopic;
        var lastPublished = new Dictionary<string, string>();

        _egressSourceRef!.Source
            .SelectMany(egressEvent => MapToMqttMessages(egressEvent, baseTopic, lastPublished))
            .WithAttributes(ActorAttributes.CreateSupervisionStrategy(
                _ => Akka.Streams.Supervision.Directive.Resume))
            .RunWith(_mqttSinkRef!.Sink, mat);
    }

    private IEnumerable<MqttMessage> MapToMqttMessages(
        EgressEvent egressEvent, string baseTopic, Dictionary<string, string> lastPublished)
    {
        var messages = egressEvent switch
        {
            EgressEvent.PerModelUpdate e => MapPerModel(e, baseTopic),
            EgressEvent.EnrichmentUpdate e when _featuresByType.TryGetValue(e.TypeName, out var feature)
                => feature.ToStateMessages(e.Result, baseTopic, e.Location),
            _ => [],
        };

        foreach (var msg in messages)
        {
            if (lastPublished.TryGetValue(msg.Topic, out var cached) && cached == msg.Payload)
            {
                continue;
            }

            lastPublished[msg.Topic] = msg.Payload;
            yield return msg;
        }
    }

    private IReadOnlyList<MqttMessage> MapPerModel(EgressEvent.PerModelUpdate e, string baseTopic)
    {
        var maxHours = ModelCoverageRegistry.Get(e.Model.Id)?.MaxForecastHours;
        var perHorizon = HorizonProjection.BuildPerHorizon(
            e.Forecast, _parameters, _horizons, _forecastDays, _timeProvider.GetUtcNow(), maxHours);

        var messages = new List<MqttMessage>(perHorizon.Count);
        foreach (var (horizon, payload) in perHorizon)
        {
            var topic = TopicScheme.HorizonTopic(baseTopic, e.Location, e.Model, horizon);
            messages.Add(new MqttMessage(topic, payload, true));
        }
        return messages;
    }
}
