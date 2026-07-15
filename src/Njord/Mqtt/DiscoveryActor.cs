using System.Reflection;
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

public sealed class DiscoveryActor : ReceiveActor, IWithStash, IWithTimers
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
    private IMaterializer? _mat;
    private readonly Dictionary<(string Location, string ModelId), ModelCapabilityLearned> _capabilities = new();
    private bool _initialDiscoveryPublished;

    public IStash Stash { get; set; } = null!;

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

        WaitingForSink();
    }

    protected override void PreStart()
    {
        if (!_discoveryEnabled)
        {
            _logger.LogInformation("MQTT discovery is disabled — DiscoveryActor idle");
            return;
        }

        _mat = Context.Materializer();

        var connectionActor = Context.GetActor<MqttConnectionActor>();
        connectionActor.Tell(new RequestMqttSink());
        connectionActor.Tell(new SubscribeInbound(Self));
    }

    private void WaitingForSink()
    {
        Receive<MqttSinkResponse>(response =>
        {
            var (queue, source) = Source.Queue<MqttMessage>(32, OverflowStrategy.DropHead)
                .PreMaterialize(_mat!);
            _queue = queue;

            source.RunWith(response.SinkRef.Sink, _mat!);

            _logger.LogInformation("DiscoveryActor ready — waiting for model capabilities");
            ScheduleCapabilityTimeout();
            Become(WaitingForCapabilities);
            Stash.UnstashAll();
        });
        ReceiveAny(_ => Stash.Stash());
    }

    private void WaitingForCapabilities()
    {
        Receive<ModelCapabilityLearned>(OnCapabilityLearned);
        Receive<CapabilityTimeout>(_ => OnCapabilityTimeout());
        Receive<MqttConnected>(_ => { });
        Receive<MqttInboundMessage>(OnInbound);
    }

    private void Ready()
    {
        Receive<ModelCapabilityLearned>(OnCapabilityUpdate);
        Receive<MqttConnected>(_ => { });
        Receive<MqttInboundMessage>(OnInbound);
    }

    private void OnCapabilityLearned(ModelCapabilityLearned msg)
    {
        _capabilities[(msg.Location, msg.Model.Id)] = msg;
        _logger.LogInformation(
            "Capability received for {Location}/{Model} ({Count}/{Expected})",
            msg.Location, msg.Model.Id, _capabilities.Count, _expectedModelCount);

        if (_capabilities.Count >= _expectedModelCount)
        {
            PublishDiscovery();
            _initialDiscoveryPublished = true;
            Become(Ready);
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
        Become(Ready);
    }

    private void OnCapabilityUpdate(ModelCapabilityLearned msg)
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

    private void PublishDiscoveryForModel(ModelCapabilityLearned cap)
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
}
