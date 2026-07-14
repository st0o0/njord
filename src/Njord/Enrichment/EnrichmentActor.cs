using Akka;
using Akka.Actor;
using Akka.Streams;
using Akka.Streams.Dsl;
using Microsoft.Extensions.Options;
using Njord.Configuration;
using Njord.Domain.Analysis;
using Njord.Domain.Weather;
using Njord.Egress;
using Njord.Mqtt;
using Njord.Ingest;
using Njord.Pipeline;
using Servus.Akka;

namespace Njord.Enrichment;

public sealed class EnrichmentActor : ReceiveActor, IWithStash
{
    private readonly NjordOptions _options;
    private readonly EnrichmentOptions _enrichmentOptions;
    private readonly ResolvedParameterSet _parameters;
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
        TimeProvider timeProvider,
        ILogger<EnrichmentActor> logger)
    {
        _options = options.Value;
        _enrichmentOptions = enrichmentOptions.Value;
        _parameters = parameters;
        _timeProvider = timeProvider;
        _logger = logger;

        WaitingForRefs();
    }

    protected override void PreStart()
    {
        _mat = Context.Materializer();

        var pipelineActor = Context.GetActor<PipelineActor>();
        Context.Watch(pipelineActor);
        pipelineActor.Tell(new RequestPipelineSource());

        var egressActor = Context.GetActor<MqttConnectionActor>();
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

        var pipelineActor = Context.GetActor<PipelineActor>();
        Context.Watch(pipelineActor);
        pipelineActor.Tell(new RequestPipelineSource());

        var egressActor = Context.GetActor<MqttConnectionActor>();
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

        if (_enrichmentOptions.Trends.Enabled)
        {
            MaterializeTrendConsumer(snapshotHubSource, mat);
        }

        if (_enrichmentOptions.Indices.Enabled)
        {
            MaterializeIndexConsumer(snapshotHubSource, mat);
        }

        if (_enrichmentOptions.Energy.Enabled)
        {
            MaterializeEnergyConsumer(snapshotHubSource, mat);
        }

        if (_enrichmentOptions.History.Enabled)
        {
            MaterializeHistoryConsumer(snapshotHubSource, mat);
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
                    foreach (var msg in StatePayloadBuilder.FromConsensus(result, baseTopic, location))
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
                    foreach (var msg in StatePayloadBuilder.FromAlerts(result, baseTopic))
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

    private void MaterializeHistoryConsumer(Source<ModelSnapshot, NotUsed> snapshotSource, IMaterializer mat)
    {
        var baseTopic = _options.Mqtt.BaseTopic;
        var parameters = _parameters;
        var locations = _options.Locations.Select(l => l.Name).ToList();
        var timeProvider = _timeProvider;
        var historyOptions = _enrichmentOptions.History;
        var lastPublished = new Dictionary<string, string>();

        var historyActors = new Dictionary<string, IActorRef>();
        foreach (var location in locations)
        {
            var actor = Context.ResolveChildActor<ForecastHistoryActor>(
                $"forecast-history-{TopicScheme.Slug(location)}",
                location, historyOptions);
            historyActors[location] = actor;
        }

        snapshotSource
            .SelectMany(snapshot =>
            {
                foreach (var (location, actor) in historyActors)
                    actor.Tell(new RecordSnapshot(snapshot));

                var messages = new List<MqttMessage>();
                foreach (var (location, actor) in historyActors)
                {
                    var response = actor.Ask<HistoryResponse>(new QueryHistory(), TimeSpan.FromSeconds(5)).Result;
                    var result = HistoryResult.Compute(
                        response.History, snapshot, location, parameters, timeProvider, historyOptions);
                    foreach (var msg in StatePayloadBuilder.FromHistory(result, baseTopic))
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

    private void MaterializeEnergyConsumer(Source<ModelSnapshot, NotUsed> snapshotSource, IMaterializer mat)
    {
        var baseTopic = _options.Mqtt.BaseTopic;
        var parameters = _parameters;
        var locations = _options.Locations.Select(l => l.Name).ToList();
        var timeProvider = _timeProvider;
        var energyOptions = _enrichmentOptions.Energy;
        var lastPublished = new Dictionary<string, string>();

        snapshotSource
            .SelectMany(snapshot =>
            {
                var messages = new List<MqttMessage>();
                foreach (var location in locations)
                {
                    var result = EnergyResult.Compute(
                        snapshot, location, parameters, timeProvider, energyOptions);
                    foreach (var msg in StatePayloadBuilder.FromEnergy(result, baseTopic))
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

    private void MaterializeIndexConsumer(Source<ModelSnapshot, NotUsed> snapshotSource, IMaterializer mat)
    {
        var baseTopic = _options.Mqtt.BaseTopic;
        var parameters = _parameters;
        var locations = _options.Locations.Select(l => l.Name).ToList();
        var timeProvider = _timeProvider;
        var indexOptions = _enrichmentOptions.Indices;
        var lastPublished = new Dictionary<string, string>();

        snapshotSource
            .SelectMany(snapshot =>
            {
                var messages = new List<MqttMessage>();
                foreach (var location in locations)
                {
                    var result = IndexResult.Compute(
                        snapshot, location, parameters, timeProvider, indexOptions);
                    foreach (var msg in StatePayloadBuilder.FromIndices(result, baseTopic))
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

    private void MaterializeTrendConsumer(Source<ModelSnapshot, NotUsed> snapshotSource, IMaterializer mat)
    {
        var baseTopic = _options.Mqtt.BaseTopic;
        var parameters = _parameters;
        var horizons = (IReadOnlyList<int>)[.. _options.Horizons];
        var locations = _options.Locations.Select(l => l.Name).ToList();
        var timeProvider = _timeProvider;
        var lastPublished = new Dictionary<string, string>();

        ModelSnapshot? previousSnapshot = null;

        snapshotSource
            .SelectMany(snapshot =>
            {
                var prev = previousSnapshot;
                previousSnapshot = snapshot;

                if (prev is null) return [];

                var messages = new List<MqttMessage>();
                foreach (var location in locations)
                {
                    var result = TrendResult.Compute(
                        snapshot, prev, location, horizons, parameters, timeProvider);
                    foreach (var msg in StatePayloadBuilder.FromTrends(result, baseTopic))
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
                    foreach (var msg in StatePayloadBuilder.FromDerived(result, baseTopic))
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
