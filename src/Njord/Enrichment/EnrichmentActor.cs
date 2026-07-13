using Akka;
using Akka.Actor;
using Akka.Hosting;
using Akka.Streams;
using Akka.Streams.Dsl;
using Microsoft.Extensions.Options;
using Njord.Configuration;
using Njord.Domain;
using Njord.Egress;
using Njord.Ingest;
using Njord.Pipeline;

namespace Njord.Enrichment;

public sealed class EnrichmentActor : ReceiveActor, IWithStash
{
    private readonly NjordOptions _options;
    private readonly EnrichmentOptions _enrichmentOptions;
    private readonly ResolvedParameterSet _parameters;
    private readonly ActorRegistry _registry;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<EnrichmentActor> _logger;

    private ISourceRef<FetchOutcome>? _sourceRef;
    private ISinkRef<MqttMessage>? _mqttSinkRef;
    private IMaterializer? _mat;

    public IStash Stash { get; set; } = null!;

    public EnrichmentActor(
        IOptions<NjordOptions> options,
        IOptions<EnrichmentOptions> enrichmentOptions,
        ResolvedParameterSet parameters,
        ActorRegistry registry,
        TimeProvider timeProvider,
        ILogger<EnrichmentActor> logger)
    {
        _options = options.Value;
        _enrichmentOptions = enrichmentOptions.Value;
        _parameters = parameters;
        _registry = registry;
        _timeProvider = timeProvider;
        _logger = logger;

        WaitingForRefs();
    }

    protected override void PreStart()
    {
        _mat = Context.Materializer();

        var pipelineActor = _registry.Get<PipelineActor>();
        Context.Watch(pipelineActor);
        pipelineActor.Tell(new RequestPipelineSource());

        var egressActor = _registry.Get<MqttEgressActor>();
        Context.Watch(egressActor);
        egressActor.Tell(new RequestMqttSink());
    }

    private void WaitingForRefs()
    {
        Receive<PipelineSourceResponse>(response =>
        {
            _sourceRef = response.SourceRef;
            _logger.LogInformation("Pipeline SourceRef received");
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
        if (_sourceRef is null || _mqttSinkRef is null) return;

        MaterializeEnrichmentGraph();
        _logger.LogInformation("Enrichment pipeline materialized — ready");
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

        _sourceRef = null;
        _mqttSinkRef = null;

        var pipelineActor = _registry.Get<PipelineActor>();
        Context.Watch(pipelineActor);
        pipelineActor.Tell(new RequestPipelineSource());

        var egressActor = _registry.Get<MqttEgressActor>();
        Context.Watch(egressActor);
        egressActor.Tell(new RequestMqttSink());

        Become(WaitingForRefs);
    }

    private void MaterializeEnrichmentGraph()
    {
        var mat = _mat!;

        var (snapshotHubSource, snapshotHubSink) = BroadcastHub.Sink<ModelSnapshot>(bufferSize: 64)
            .PreMaterialize(mat);

        _sourceRef!.Source
            .Scan(ModelSnapshot.Empty, (snap, outcome) => outcome switch
            {
                FetchOutcome.Success s => snap.Update(s.Forecast),
                _ => snap,
            })
            .Where(snap => snap.HasChanged)
            .To(snapshotHubSink)
            .Run(mat);

        if (_enrichmentOptions.Consensus.Enabled)
        {
            MaterializeConsensusConsumer(snapshotHubSource, mat);
        }

        if (_enrichmentOptions.Alerts.Enabled)
        {
            MaterializeAlertConsumer(snapshotHubSource, mat);
        }

        if (_enrichmentOptions.Derived.Enabled)
        {
            MaterializeDerivedConsumer(snapshotHubSource, mat);
        }
    }

    private void MaterializeConsensusConsumer(Source<ModelSnapshot, NotUsed> snapshotSource, IMaterializer mat)
    {
        var baseTopic = _options.Mqtt.BaseTopic;
        var parameters = _parameters;
        var horizons = (IReadOnlyList<int>)[.. _options.Horizons];
        var locations = _options.Locations.Select(l => l.Name).ToList();
        var timeProvider = _timeProvider;
        var trimPercent = _enrichmentOptions.Consensus.TrimPercent;
        var lastPublished = new Dictionary<string, string>();

        snapshotSource
            .SelectMany(snapshot =>
            {
                var messages = new List<MqttMessage>();
                foreach (var location in locations)
                {
                    var result = ConsensusResult.Compute(
                        snapshot, parameters, horizons, location, timeProvider, trimPercent);
                    foreach (var msg in result.ToMqttMessages(baseTopic, location))
                    {
                        if (lastPublished.TryGetValue(msg.Topic, out var cached) && cached == msg.Payload)
                            continue;
                        lastPublished[msg.Topic] = msg.Payload;
                        messages.Add(msg);
                    }
                }
                return messages;
            })
            .WithAttributes(ActorAttributes.CreateSupervisionStrategy(
                _ => Akka.Streams.Supervision.Directive.Resume))
            .RunWith(_mqttSinkRef!.Sink, mat);
    }

    private void MaterializeAlertConsumer(Source<ModelSnapshot, NotUsed> snapshotSource, IMaterializer mat)
    {
        var baseTopic = _options.Mqtt.BaseTopic;
        var locations = _options.Locations.Select(l => l.Name).ToList();
        var timeProvider = _timeProvider;
        var alertOptions = _enrichmentOptions.Alerts;
        var lastPublished = new Dictionary<string, string>();

        snapshotSource
            .SelectMany(snapshot =>
            {
                var messages = new List<MqttMessage>();
                foreach (var location in locations)
                {
                    var result = AlertEvaluator.EvaluateAll(snapshot, location, alertOptions, timeProvider);
                    foreach (var msg in result.ToMqttMessages(baseTopic))
                    {
                        if (lastPublished.TryGetValue(msg.Topic, out var cached) && cached == msg.Payload)
                            continue;
                        lastPublished[msg.Topic] = msg.Payload;
                        messages.Add(msg);
                    }
                }
                return messages;
            })
            .WithAttributes(ActorAttributes.CreateSupervisionStrategy(
                _ => Akka.Streams.Supervision.Directive.Resume))
            .RunWith(_mqttSinkRef!.Sink, mat);
    }

    private void MaterializeDerivedConsumer(Source<ModelSnapshot, NotUsed> snapshotSource, IMaterializer mat)
    {
        var baseTopic = _options.Mqtt.BaseTopic;
        var parameters = _parameters;
        var horizons = (IReadOnlyList<int>)[.. _options.Horizons];
        var locations = _options.Locations.Select(l => l.Name).ToList();
        var timeProvider = _timeProvider;
        var lastPublished = new Dictionary<string, string>();

        snapshotSource
            .SelectMany(snapshot =>
            {
                var messages = new List<MqttMessage>();
                foreach (var location in locations)
                {
                    var result = DerivedResult.Compute(
                        snapshot, location, horizons, parameters, timeProvider);
                    foreach (var msg in result.ToMqttMessages(baseTopic))
                    {
                        if (lastPublished.TryGetValue(msg.Topic, out var cached) && cached == msg.Payload)
                            continue;
                        lastPublished[msg.Topic] = msg.Payload;
                        messages.Add(msg);
                    }
                }
                return messages;
            })
            .WithAttributes(ActorAttributes.CreateSupervisionStrategy(
                _ => Akka.Streams.Supervision.Directive.Resume))
            .RunWith(_mqttSinkRef!.Sink, mat);
    }
}
