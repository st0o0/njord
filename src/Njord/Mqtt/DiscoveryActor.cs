using System.Reflection;
using Akka.Actor;
using Akka.Streams;
using Akka.Streams.Dsl;
using Microsoft.Extensions.Options;
using Njord.Configuration;
using Njord.Domain.Weather;
using Njord.Egress;
using Servus.Akka;

namespace Njord.Mqtt;

public sealed class DiscoveryActor : ReceiveActor, IWithStash
{
    private static readonly string Version =
        typeof(DiscoveryActor).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()
            ?.InformationalVersion ?? "unknown";

    private readonly NjordOptions _options;
    private readonly EnrichmentOptions _enrichmentOptions;
    private readonly ResolvedParameterSet _parameters;
    private readonly ILogger<DiscoveryActor> _logger;
    private readonly IReadOnlyList<int> _horizons;
    private readonly string _haStatusTopic;
    private readonly bool _discoveryEnabled;

    private ISourceQueueWithComplete<MqttMessage>? _queue;
    private IMaterializer? _mat;

    public IStash Stash { get; set; } = null!;

    public DiscoveryActor(
        IOptions<NjordOptions> options,
        IOptions<EnrichmentOptions> enrichmentOptions,
        ResolvedParameterSet parameters,
        ILogger<DiscoveryActor> logger)
    {
        _options = options.Value;
        _enrichmentOptions = enrichmentOptions.Value;
        _parameters = parameters;
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
        foreach (var location in _options.Locations)
        {
            foreach (var modelId in _options.Models)
            {
                var model = new WeatherModel(modelId);
                var topic = TopicScheme.ConfigTopic(
                    _options.Mqtt.DiscoveryPrefix, TopicScheme.DeviceId(location.Name, model));
                var payload = DiscoveryPayloadBuilder.Build(
                    location.Name, model, _parameters, _horizons, _options.ForecastDays,
                    _options.Mqtt, _options.PollInterval, Version);
                _queue?.OfferAsync(new MqttMessage(topic, payload, true));
            }

            if (_enrichmentOptions.Consensus.Enabled)
            {
                var consensusDeviceId = TopicScheme.ConsensusDeviceId(location.Name);
                var consensusTopic = TopicScheme.ConfigTopic(_options.Mqtt.DiscoveryPrefix, consensusDeviceId);
                var consensusPayload = DiscoveryPayloadBuilder.BuildConsensus(
                    location.Name, _parameters, _horizons, _options.ForecastDays,
                    _options.Mqtt, _options.PollInterval, Version);
                _queue?.OfferAsync(new MqttMessage(consensusTopic, consensusPayload, true));
            }

            if (_enrichmentOptions.Alerts.Enabled)
            {
                var alertDeviceId = TopicScheme.AlertDeviceId(location.Name);
                var alertTopic = TopicScheme.ConfigTopic(_options.Mqtt.DiscoveryPrefix, alertDeviceId);
                var alertPayload = DiscoveryPayloadBuilder.BuildAlerts(
                    location.Name, _options.Mqtt, _options.PollInterval, Version);
                _queue?.OfferAsync(new MqttMessage(alertTopic, alertPayload, true));
            }

            if (_enrichmentOptions.Derived.Enabled)
            {
                var derivedDeviceId = TopicScheme.DerivedDeviceId(location.Name);
                var derivedTopic = TopicScheme.ConfigTopic(_options.Mqtt.DiscoveryPrefix, derivedDeviceId);
                var derivedPayload = DiscoveryPayloadBuilder.BuildDerived(
                    location.Name, _horizons, _options.Mqtt, _options.PollInterval, Version);
                _queue?.OfferAsync(new MqttMessage(derivedTopic, derivedPayload, true));
            }

            if (_enrichmentOptions.Trends.Enabled)
            {
                var trendDeviceId = TopicScheme.TrendDeviceId(location.Name);
                var trendTopic = TopicScheme.ConfigTopic(_options.Mqtt.DiscoveryPrefix, trendDeviceId);
                var trendPayload = DiscoveryPayloadBuilder.BuildTrends(
                    location.Name, _options.Mqtt, _options.PollInterval, Version);
                _queue?.OfferAsync(new MqttMessage(trendTopic, trendPayload, true));
            }

            if (_enrichmentOptions.Indices.Enabled)
            {
                var indexDeviceId = TopicScheme.IndexDeviceId(location.Name);
                var indexTopic = TopicScheme.ConfigTopic(_options.Mqtt.DiscoveryPrefix, indexDeviceId);
                var indexPayload = DiscoveryPayloadBuilder.BuildIndices(
                    location.Name, _options.Mqtt, _options.PollInterval, Version);
                _queue?.OfferAsync(new MqttMessage(indexTopic, indexPayload, true));
            }

            if (_enrichmentOptions.Energy.Enabled)
            {
                var energyDeviceId = TopicScheme.EnergyDeviceId(location.Name);
                var energyConfTopic = TopicScheme.ConfigTopic(_options.Mqtt.DiscoveryPrefix, energyDeviceId);
                var energyPayload = DiscoveryPayloadBuilder.BuildEnergy(
                    location.Name, _options.Mqtt, _options.PollInterval, Version);
                _queue?.OfferAsync(new MqttMessage(energyConfTopic, energyPayload, true));
            }

            if (_enrichmentOptions.History.Enabled)
            {
                var historyDeviceId = TopicScheme.HistoryDeviceId(location.Name);
                var historyConfTopic = TopicScheme.ConfigTopic(_options.Mqtt.DiscoveryPrefix, historyDeviceId);
                var historyPayload = DiscoveryPayloadBuilder.BuildHistory(
                    location.Name, [.. _options.Models], _options.Mqtt, _options.PollInterval, Version);
                _queue?.OfferAsync(new MqttMessage(historyConfTopic, historyPayload, true));
            }
        }
    }

    protected override void PostStop()
    {
        _queue?.Complete();
    }
}
