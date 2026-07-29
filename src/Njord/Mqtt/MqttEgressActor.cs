using Akka.Actor;
using Akka.Streams;
using Akka.Streams.Dsl;
using Microsoft.Extensions.Options;
using Njord.Actors;
using Njord.Configuration;
using Njord.Domain.Weather;
using Njord.Egress;
using Njord.Enrichment;
using Njord.Pipeline;
using Servus.Akka;

namespace Njord.Mqtt;

public sealed class MqttEgressActor : StreamConsumerActor
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

    private sealed record EgressResolved(IActorRef Ref);
    private sealed record ConnectionResolved(IActorRef Ref);

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
    }

    protected override void ResolveDependencies()
    {
        Context.GetActorAsync<EgressActor>().PipeTo(Self, success: r => new EgressResolved(r));
        Context.GetActorAsync<MqttConnectionActor>().PipeTo(Self, success: r => new ConnectionResolved(r));
    }

    protected override void ConfigureWaitingForRefs()
    {
        Receive<EgressResolved>(msg =>
        {
            if (IsDeadRef(msg.Ref)) { ScheduleRetryResolve(); return; }
            TrackDependency(msg.Ref);
            msg.Ref.Tell(new RequestEgressSource());
        });
        Receive<ConnectionResolved>(msg =>
        {
            if (IsDeadRef(msg.Ref)) { ScheduleRetryResolve(); return; }
            TrackDependency(msg.Ref);
            msg.Ref.Tell(new RequestMqttSink());
        });
        Receive<EgressSourceResponse>(response =>
        {
            _egressSourceRef = response.SourceRef;
            _logger.LogInformation("Egress SourceRef received");
            TryTransition();
        });
        Receive<MqttSinkResponse>(response =>
        {
            _mqttSinkRef = response.SinkRef;
            _logger.LogInformation("MQTT SinkRef received");
            TryTransition();
        });
    }

    protected override bool AllRefsReady() => _egressSourceRef is not null && _mqttSinkRef is not null;

    protected override void MaterializeGraph(SharedKillSwitch killSwitch)
    {
        var baseTopic = _baseTopic;
        var lastPublished = new Dictionary<string, int>();

        _egressSourceRef!.Source
            .Via(killSwitch.Flow<EgressEvent>())
            .SelectMany(egressEvent => MapToMqttMessages(egressEvent, baseTopic, lastPublished))
            .WithAttributes(ActorAttributes.CreateSupervisionStrategy(StreamSupervision.LoggingDecider(_logger)))
            .RunWith(_mqttSinkRef!.Sink, Mat);
    }

    protected override void OnDependencyLost()
    {
        _mqttSinkRef = null;
        _egressSourceRef = null;
    }

    private IEnumerable<MqttMessage> MapToMqttMessages(
        EgressEvent egressEvent, string baseTopic, Dictionary<string, int> lastPublished)
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
            var hash = msg.Payload.GetHashCode();
            if (lastPublished.TryGetValue(msg.Topic, out var cached) && cached == hash)
            {
                continue;
            }

            lastPublished[msg.Topic] = hash;
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
