using System.Reflection;
using Akka.Actor;
using Akka.Streams;
using Akka.Streams.Dsl;
using Microsoft.Extensions.Options;
using Njord.Configuration;
using Njord.Domain.Weather;
using Njord.Enrichment;
using Njord.Telemetry;
using Servus.Akka;

namespace Njord.Mqtt;

public sealed class DiscoveryActor : ReceiveActor, IWithStash
{
    private static readonly string Version =
        typeof(DiscoveryActor).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()
            ?.InformationalVersion ?? "unknown";

    private readonly NjordOptions _options;
    private readonly ResolvedParameterSet _parameters;
    private readonly IReadOnlyList<IEnrichmentFeature> _features;
    private readonly ILogger<DiscoveryActor> _logger;
    private readonly IReadOnlyList<int> _horizons;
    private readonly string _haStatusTopic;
    private readonly bool _discoveryEnabled;

    private ISourceQueueWithComplete<MqttMessage>? _queue;
    private IMaterializer? _mat;

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
        _horizons = [.. _options.Horizons];
        _haStatusTopic = $"{_options.Mqtt.DiscoveryPrefix}/status";
        _discoveryEnabled = _options.Mqtt.DiscoveryEnabled;

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

            _logger.LogInformation("DiscoveryActor ready — SinkRef connected");
            Become(Ready);
            Stash.UnstashAll();
        });
        ReceiveAny(_ => Stash.Stash());
    }

    private void Ready()
    {
        Receive<MqttConnected>(_ => PublishDiscovery());
        Receive<MqttInboundMessage>(OnInbound);
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
        var count = 0;
        var ctx = new DiscoveryContext(_options.Mqtt, _options.PollInterval, Version);

        foreach (var location in _options.Locations)
        {
            foreach (var modelId in location.ResolveModels(_options.Models))
            {
                var model = new WeatherModel(modelId);
                var topic = TopicScheme.ConfigTopic(
                    _options.Mqtt.DiscoveryPrefix, TopicScheme.DeviceId(location.Name, model));
                var payload = DiscoveryPayloadBuilder.Build(
                    location.Name, model, _parameters, _horizons, _options.ForecastDays,
                    _options.Mqtt, _options.PollInterval, Version);
                _queue?.OfferAsync(new MqttMessage(topic, payload, true));
                count++;
            }

            foreach (var feature in _features)
            {
                if (!feature.Enabled) continue;

                var deviceId = feature.DeviceId(location.Name);
                var topic = TopicScheme.ConfigTopic(_options.Mqtt.DiscoveryPrefix, deviceId);
                var payload = feature.BuildDiscoveryPayload(ctx, location.Name);
                _queue?.OfferAsync(new MqttMessage(topic, payload, true));
                count++;
            }
        }

        NjordTelemetry.DiscoveryPublishes.Add(count);
    }

    protected override void PostStop()
    {
        _queue?.Complete();
    }
}
