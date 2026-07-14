using Akka.Actor;
using Akka.Streams;
using Akka.Streams.Dsl;
using Microsoft.Extensions.Options;
using Njord.Configuration;
using Njord.Egress;
using Servus.Akka;

namespace Njord.Mqtt;

public sealed class MqttEgressActor : ReceiveActor, IWithStash
{
    private readonly string _baseTopic;
    private readonly ILogger<MqttEgressActor> _logger;

    private ISinkRef<MqttMessage>? _mqttSinkRef;
    private ISourceRef<EgressEvent>? _egressSourceRef;
    private IMaterializer? _mat;

    public IStash Stash { get; set; } = null!;

    public MqttEgressActor(
        IOptions<NjordOptions> options,
        ILogger<MqttEgressActor> logger)
    {
        _baseTopic = options.Value.Mqtt.BaseTopic;
        _logger = logger;

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
        Receive<Terminated>(msg => HandleTerminated(msg));
        ReceiveAny(_ => Stash.Stash());
    }

    private void TryTransitionToReady()
    {
        if (_egressSourceRef is null || _mqttSinkRef is null) return;

        MaterializeGraph();
        _logger.LogInformation("MQTT egress pipeline materialized — ready");
        Become(Ready);
        Stash.UnstashAll();
    }

    private void Ready()
    {
        Receive<Terminated>(msg => HandleTerminated(msg));
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

    private static IEnumerable<MqttMessage> MapToMqttMessages(
        EgressEvent egressEvent, string baseTopic, Dictionary<string, string> lastPublished)
    {
        var messages = egressEvent switch
        {
            EgressEvent.PerModelUpdate e => MapPerModel(e, baseTopic),
            EgressEvent.ConsensusUpdate e => StatePayloadBuilder.FromConsensus(e.Result, baseTopic, e.Location),
            EgressEvent.AlertUpdate e => StatePayloadBuilder.FromAlerts(e.Result, baseTopic),
            EgressEvent.DerivedUpdate e => StatePayloadBuilder.FromDerived(e.Result, baseTopic),
            EgressEvent.TrendUpdate e => StatePayloadBuilder.FromTrends(e.Result, baseTopic),
            EgressEvent.IndexUpdate e => StatePayloadBuilder.FromIndices(e.Result, baseTopic),
            EgressEvent.EnergyUpdate e => StatePayloadBuilder.FromEnergy(e.Result, baseTopic),
            EgressEvent.HistoryUpdate e => StatePayloadBuilder.FromHistory(e.Result, baseTopic),
            _ => [],
        };

        foreach (var msg in messages)
        {
            if (lastPublished.TryGetValue(msg.Topic, out var cached) && cached == msg.Payload)
                continue;
            lastPublished[msg.Topic] = msg.Payload;
            yield return msg;
        }
    }

    private static IReadOnlyList<MqttMessage> MapPerModel(EgressEvent.PerModelUpdate e, string baseTopic)
    {
        var messages = new List<MqttMessage>(e.HorizonPayloads.Count);
        foreach (var (horizon, payload) in e.HorizonPayloads)
        {
            var topic = TopicScheme.HorizonTopic(baseTopic, e.Location, e.Model, horizon);
            messages.Add(new MqttMessage(topic, payload, true));
        }
        return messages;
    }
}
