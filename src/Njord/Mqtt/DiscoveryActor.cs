using System.Reflection;
using Akka.Actor;
using Akka.Streams;
using Akka.Streams.Dsl;
using Microsoft.Extensions.Options;
using Njord.Actors;
using Njord.Configuration;
using Njord.Domain.Weather;
using Njord.Egress;
using Njord.Enrichment;
using Servus.Akka;

namespace Njord.Mqtt;

public sealed class DiscoveryActor : StreamConsumerActor, IWithTimers
{
    public ITimerScheduler Timers { get; set; } = null!;
    private static readonly string Version =
        typeof(DiscoveryActor).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()
            ?.InformationalVersion ?? "unknown";

    private readonly NjordOptions _options;
    private readonly ResolvedParameterSet _parameters;
    private readonly IReadOnlyList<IEnrichmentFeature> _features;
    private readonly ILogger<DiscoveryActor> _logger;
    private readonly string _haStatusTopic;
    private readonly bool _discoveryEnabled;
    private readonly int _expectedModelCount;

    private ISourceQueueWithComplete<MqttMessage>? _queue;
    private ISinkRef<MqttMessage>? _mqttSinkRef;
    private ISourceRef<EgressEvent>? _egressSourceRef;
    private readonly Dictionary<(string Location, string ModelId), EgressEvent.CapabilityLearned> _capabilities = new();
    private bool _initialDiscoveryPublished;

    private sealed record ConnectionResolved(IActorRef Ref);
    private sealed record EgressResolved(IActorRef Ref);
    private sealed record StreamCompleted
    {
        public static readonly StreamCompleted Instance = new();
    }

    public DiscoveryActor(
        IOptions<NjordOptions> options,
        ResolvedParameterSet parameters,
        IEnumerable<IEnrichmentFeature> features,
        ILogger<DiscoveryActor> logger)
    {
        _options = options.Value;
        _parameters = parameters;
        _features = [.. features];
        _logger = logger;
        _haStatusTopic = $"{_options.Mqtt.DiscoveryPrefix}/status";
        _discoveryEnabled = _options.Mqtt.DiscoveryEnabled;

        _expectedModelCount = _options.Locations
            .Sum(loc => loc.ResolveModels(_options.Models).Count);
    }

    protected override void PreStart()
    {
        if (!_discoveryEnabled)
        {
            _logger.LogInformation("MQTT discovery is disabled — DiscoveryActor idle");
            return;
        }

        base.PreStart();
    }

    protected override void ResolveDependencies()
    {
        Context.GetActorAsync<MqttConnectionActor>().PipeTo(Self, success: r => new ConnectionResolved(r));
        Context.GetActorAsync<EgressActor>().PipeTo(Self, success: r => new EgressResolved(r));
    }

    protected override void ConfigureWaitingForRefs()
    {
        Receive<ConnectionResolved>(msg =>
        {
            if (IsDeadRef(msg.Ref)) { ScheduleRetryResolve(); return; }
            TrackDependency(msg.Ref);
            msg.Ref.Tell(new RequestMqttSink());
            msg.Ref.Tell(new SubscribeInbound(Self));
        });
        Receive<EgressResolved>(msg =>
        {
            if (IsDeadRef(msg.Ref)) { ScheduleRetryResolve(); return; }
            TrackDependency(msg.Ref);
            msg.Ref.Tell(new RequestEgressSource());
        });
        Receive<MqttSinkResponse>(response =>
        {
            _mqttSinkRef = response.SinkRef;
            _logger.LogInformation("MQTT SinkRef received");
            TryTransition();
        });
        Receive<EgressSourceResponse>(response =>
        {
            _egressSourceRef = response.SourceRef;
            _logger.LogInformation("Egress SourceRef received");
            TryTransition();
        });
    }

    protected override bool AllRefsReady() => _mqttSinkRef is not null && _egressSourceRef is not null;

    protected override void MaterializeGraph(SharedKillSwitch killSwitch)
    {
        var (queue, source) = Source.Queue<MqttMessage>(32, OverflowStrategy.DropHead)
            .PreMaterialize(Mat);
        _queue = queue;

        source
            .Via(killSwitch.Flow<MqttMessage>())
            .RunWith(_mqttSinkRef!.Sink, Mat);

        var self = Self;
        _egressSourceRef!.Source
            .Via(killSwitch.Flow<EgressEvent>())
            .Where(e => e is EgressEvent.CapabilityLearned)
            .Select(e => new CapabilityReceived((EgressEvent.CapabilityLearned)e))
            .RunWith(Sink.ActorRef<CapabilityReceived>(self, StreamCompleted.Instance, _ => StreamCompleted.Instance), Mat);
    }

    protected override void ConfigureReady()
    {
        _logger.LogInformation("DiscoveryActor ready — waiting for model capabilities");
        ScheduleCapabilityTimeout();

        Receive<CapabilityReceived>(msg =>
        {
            if (!_initialDiscoveryPublished)
            {
                OnCapabilityLearned(msg.Event);
            }
            else
            {
                OnCapabilityUpdate(msg.Event);
            }
        });
        Receive<CapabilityTimeout>(_ => OnCapabilityTimeout());
        Receive<MqttConnected>(_ => { });
        Receive<MqttInboundMessage>(OnInbound);
        Receive<StreamCompleted>(_ => { });
    }

    protected override void OnDependencyLost()
    {
        _mqttSinkRef = null;
        _egressSourceRef = null;
        _queue?.Complete();
        _queue = null;
    }

    private void OnCapabilityLearned(EgressEvent.CapabilityLearned msg)
    {
        _capabilities[(msg.Location, msg.Model.Id)] = msg;
        _logger.LogInformation(
            "Capability received for {Location}/{Model} ({Count}/{Expected})",
            msg.Location, msg.Model.Id, _capabilities.Count, _expectedModelCount);

        if (_capabilities.Count >= _expectedModelCount)
        {
            PublishDiscovery();
            _initialDiscoveryPublished = true;
        }
    }

    private void OnCapabilityTimeout()
    {
        if (_initialDiscoveryPublished)
        {
            return;
        }

        _logger.LogWarning(
            "Capability timeout — publishing discovery for {Count}/{Expected} models",
            _capabilities.Count, _expectedModelCount);

        PublishDiscovery();
        _initialDiscoveryPublished = true;
    }

    private void OnCapabilityUpdate(EgressEvent.CapabilityLearned msg)
    {
        var key = (msg.Location, msg.Model.Id);
        var isNew = !_capabilities.ContainsKey(key);
        _capabilities[key] = msg;

        if (isNew)
        {
            _logger.LogInformation("Late capability for {Location}/{Model} — publishing discovery", msg.Location, msg.Model.Id);
        }
        else
        {
            _logger.LogInformation("Capability expanded for {Location}/{Model} — re-publishing discovery", msg.Location, msg.Model.Id);
        }

        PublishDiscoveryForModel(msg);
    }

    private void OnInbound(MqttInboundMessage message)
    {
        if (message.Topic == _haStatusTopic && message.Payload == "online")
        {
            _logger.LogInformation("Home Assistant is back online — re-publishing discovery");
            PublishDiscovery();
        }
    }

    private void PublishDiscovery()
    {
        var ctx = new DiscoveryContext(_options.Mqtt, _options.PollInterval, Version);

        foreach (var location in _options.Locations)
        {
            foreach (var modelId in location.ResolveModels(_options.Models))
            {
                var key = (location.Name, modelId);
                if (!_capabilities.TryGetValue(key, out var cap))
                {
                    continue;
                }

                PublishDiscoveryForModel(cap);
            }

            {
                var consensusDeviceId = TopicScheme.EnrichmentDeviceId(location.Name, "consensus");
                var consensusTopic = TopicScheme.ConfigTopic(_options.Mqtt.DiscoveryPrefix, consensusDeviceId);
                var consensusPayload = DiscoveryPayloadBuilder.BuildConsensus(
                    location.Name, _parameters,
                    _options.ForecastDays * 24, _options.ForecastDays,
                    _options.Mqtt, _options.PollInterval, Version);
                _queue?.OfferAsync(new MqttMessage(consensusTopic, consensusPayload, true));
            }

            foreach (var feature in _features)
            {
                if (!feature.Enabled)
                {
                    continue;
                }

                var deviceId = feature.DeviceId(location.Name);
                var topic = TopicScheme.ConfigTopic(_options.Mqtt.DiscoveryPrefix, deviceId);
                var payload = feature.BuildDiscoveryPayload(ctx, location.Name);
                _queue?.OfferAsync(new MqttMessage(topic, payload, true));
            }
        }
    }

    private void PublishDiscoveryForModel(EgressEvent.CapabilityLearned cap)
    {
        var model = cap.Model;
        var topic = TopicScheme.ConfigTopic(
            _options.Mqtt.DiscoveryPrefix, TopicScheme.DeviceId(cap.Location, model));
        var payload = DiscoveryPayloadBuilder.Build(
            cap.Location, model, _parameters,
            cap.ApplicableHorizons, cap.ApplicableDayOffsets,
            cap.SupportedParameters,
            _options.Mqtt, _options.PollInterval, Version);
        _queue?.OfferAsync(new MqttMessage(topic, payload, true));
    }

    private void ScheduleCapabilityTimeout()
    {
        var timeout = _options.PollInterval + _options.PollInterval;
        Timers.StartSingleTimer("capability-timeout", new CapabilityTimeout(), timeout);
    }

    protected override void PostStop()
    {
        _queue?.Complete();
    }

    private sealed record CapabilityTimeout;
    private sealed record CapabilityReceived(EgressEvent.CapabilityLearned Event);
}
