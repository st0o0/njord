using System.Reflection;
using Akka;
using Akka.Actor;
using Akka.Hosting;
using Akka.Streams;
using Akka.Streams.Dsl;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Njord.Configuration;
using Njord.Domain;
using Njord.Ingest;
using Njord.Pipeline;

namespace Njord.Egress;

public sealed class MqttEgressActor : ReceiveActor, IWithStash
{
    private static readonly string Version =
        typeof(MqttEgressActor).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()
            ?.InformationalVersion ?? "unknown";

    private readonly NjordOptions _options;
    private readonly IMqttConnection _connection;
    private readonly IMqttTransport _transport;
    private readonly ILogger<MqttEgressActor> _logger;
    private readonly MqttEgressTuning _tuning;
    private readonly ResolvedParameterSet _parameters;
    private readonly ActorRegistry _registry;
    private readonly IReadOnlyList<int> _horizons;
    private readonly string _availabilityTopic;
    private readonly string _haStatusTopic;
    private readonly string _deviceConfigFilter;
    private readonly HashSet<string> _ownConfigTopics;
    private int _connectAttempts;

    private ISourceQueueWithComplete<MqttMessage>? _discoveryQueue;
    private ISourceQueueWithComplete<MqttMessage>? _availabilityQueue;
    private ISourceQueueWithComplete<MqttMessage>? _tombstoneQueue;
    private Sink<MqttMessage, NotUsed>? _mergeHubSink;
    private IMaterializer? _mat;

    public IStash Stash { get; set; } = null!;

    private sealed record Connected;
    private sealed record ConnectFailed(Exception Cause);
    private sealed record Disconnected;
    private sealed record Reconnect;
    private sealed record Inbound(string Topic, string Payload);

    public MqttEgressActor(
        IOptions<NjordOptions> options,
        IMqttConnection connection,
        IMqttTransport transport,
        ILogger<MqttEgressActor> logger,
        MqttEgressTuning tuning,
        ResolvedParameterSet parameters,
        ActorRegistry registry)
    {
        _options = options.Value;
        _connection = connection;
        _transport = transport;
        _logger = logger;
        _tuning = tuning;
        _parameters = parameters;
        _registry = registry;
        _horizons = [.. _options.Horizons];
        _availabilityTopic = TopicScheme.AvailabilityTopic(_options.Mqtt.BaseTopic);
        _haStatusTopic = $"{_options.Mqtt.DiscoveryPrefix}/status";
        _deviceConfigFilter = $"{_options.Mqtt.DiscoveryPrefix}/device/+/config";
        _ownConfigTopics =
        [
            .. from location in _options.Locations
               from modelId in _options.Models
               select TopicScheme.ConfigTopic(
                   _options.Mqtt.DiscoveryPrefix,
                   TopicScheme.DeviceId(location.Name, new WeatherModel(modelId))),
        ];

        WaitingForSourceRef();
    }

    protected override void PreStart()
    {
        _mat = Context.Materializer();
        MaterializeEgressGraph(_mat);

        var pipelineActor = _registry.Get<PipelineActor>();
        Context.Watch(pipelineActor);
        pipelineActor.Tell(new RequestPipelineSource());
    }

    private void WaitingForSourceRef()
    {
        Receive<PipelineSourceResponse>(response =>
        {
            MaterializeConsumerGraph(response.SourceRef);
            _logger.LogInformation("Pipeline SourceRef received — egress consumer connected");
            Become(Ready);
            Stash.UnstashAll();
            Connect();
        });
        Receive<Terminated>(_ => RequestNewSourceRef());
        ReceiveAny(_ => Stash.Stash());
    }

    private void Ready()
    {
        ReceiveAsync<Connected>(OnConnectedAsync);
        Receive<ConnectFailed>(msg =>
        {
            _logger.LogWarning(msg.Cause, "MQTT connect to {Host}:{Port} failed", _options.Mqtt.Host, _options.Mqtt.Port);
            ScheduleReconnect();
        });
        Receive<Disconnected>(_ =>
        {
            _logger.LogWarning("MQTT connection lost — reconnecting");
            ScheduleReconnect();
        });
        Receive<Reconnect>(_ => Connect());
        ReceiveAsync<Inbound>(OnInboundAsync);
        Receive<Terminated>(_ =>
        {
            _logger.LogWarning("PipelineActor terminated — waiting for new SourceRef");
            RequestNewSourceRef();
        });
    }

    protected override void PostStop()
    {
        _availabilityQueue?.OfferAsync(new MqttMessage(_availabilityTopic, "offline", true));
        _discoveryQueue?.Complete();
        _availabilityQueue?.Complete();
        _tombstoneQueue?.Complete();
    }

    private void MaterializeEgressGraph(IMaterializer mat)
    {
        var (discQueue, discSource) = Source.Queue<MqttMessage>(32, OverflowStrategy.DropHead)
            .PreMaterialize(mat);
        var (availQueue, availSource) = Source.Queue<MqttMessage>(8, OverflowStrategy.DropHead)
            .PreMaterialize(mat);
        var (tombQueue, tombSource) = Source.Queue<MqttMessage>(16, OverflowStrategy.DropHead)
            .PreMaterialize(mat);

        _discoveryQueue = discQueue;
        _availabilityQueue = availQueue;
        _tombstoneQueue = tombQueue;

        var (hubSink, hubSource) = MergeHub.Source<MqttMessage>(perProducerBufferSize: 8)
            .PreMaterialize(mat);

        _mergeHubSink = hubSink;

        discSource.RunWith(hubSink, mat);
        availSource.RunWith(hubSink, mat);
        tombSource.RunWith(hubSink, mat);

        hubSource
            .SelectAsync(1, async msg =>
            {
                await _transport.SendAsync(msg.Topic, msg.Payload, msg.Retain, CancellationToken.None);
                return NotUsed.Instance;
            })
            .WithAttributes(ActorAttributes.CreateSupervisionStrategy(
                _ => Akka.Streams.Supervision.Directive.Resume))
            .RunWith(Sink.Ignore<NotUsed>(), mat);
    }

    private void MaterializeConsumerGraph(ISourceRef<FetchOutcome.Success> sourceRef)
    {
        var mat = _mat!;
        var baseTopic = _options.Mqtt.BaseTopic;
        var horizons = _horizons;
        var forecastDays = _options.ForecastDays;
        var parameters = _parameters;
        var lastPublished = new Dictionary<(string, string, string), string>();

        sourceRef.Source
            .SelectMany(success =>
            {
                var forecast = success.Forecast;
                var perHorizon = StatePayloadBuilder.BuildPerHorizon(forecast, parameters, horizons, forecastDays);
                var messages = new List<MqttMessage>();

                foreach (var (horizon, payload) in perHorizon)
                {
                    var key = (forecast.Location, forecast.Model.Id, horizon);
                    if (lastPublished.TryGetValue(key, out var cached) && cached == payload)
                        continue;

                    lastPublished[key] = payload;
                    var topic = TopicScheme.HorizonTopic(baseTopic, forecast.Location, forecast.Model, horizon);
                    messages.Add(new MqttMessage(topic, payload, true));
                }

                return messages;
            })
            .WithAttributes(ActorAttributes.CreateSupervisionStrategy(
                _ => Akka.Streams.Supervision.Directive.Resume))
            .RunWith(_mergeHubSink!, mat);
    }

    private void RequestNewSourceRef()
    {
        var pipelineActor = _registry.Get<PipelineActor>();
        Context.Watch(pipelineActor);
        pipelineActor.Tell(new RequestPipelineSource());
    }

    private void Connect()
    {
        var self = Self;
        _connection
            .ConnectAsync(
                (topic, payload) => self.Tell(new Inbound(topic, payload)),
                () => self.Tell(new Disconnected()),
                CancellationToken.None)
            .ContinueWith(t => t.IsCompletedSuccessfully
                ? (object)new Connected()
                : new ConnectFailed(t.Exception?.GetBaseException() ?? new InvalidOperationException("connect canceled")))
            .PipeTo(self);
    }

    private void ScheduleReconnect()
    {
        _connectAttempts++;
        var factor = Math.Pow(2, Math.Min(_connectAttempts - 1, 6));
        var delay = TimeSpan.FromMilliseconds(_tuning.ReconnectDelay.TotalMilliseconds * factor);
        Context.System.Scheduler.ScheduleTellOnce(delay, Self, new Reconnect(), Self);
    }

    private async Task OnConnectedAsync(Connected _)
    {
        _connectAttempts = 0;
        try
        {
            await _connection.SubscribeAsync(_haStatusTopic, CancellationToken.None);
            await _connection.SubscribeAsync(_deviceConfigFilter, CancellationToken.None);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Post-connect subscription failed");
        }

        _availabilityQueue?.OfferAsync(new MqttMessage(_availabilityTopic, "online", true));
        PublishDiscovery();
    }

    private async Task OnInboundAsync(Inbound message)
    {
        if (message.Topic == _haStatusTopic)
        {
            if (message.Payload == "online")
            {
                _logger.LogInformation("Home Assistant is back online — re-publishing discovery");
                PublishDiscovery();
            }
            return;
        }

        var njordDevicePrefix = $"{_options.Mqtt.DiscoveryPrefix}/device/njord_";
        if (message.Topic.StartsWith(njordDevicePrefix, StringComparison.Ordinal)
            && message.Topic.EndsWith("/config", StringComparison.Ordinal)
            && message.Payload.Length > 0
            && !_ownConfigTopics.Contains(message.Topic))
        {
            _logger.LogInformation("Tombstoning stale retained device config {Topic}", message.Topic);
            _tombstoneQueue?.OfferAsync(new MqttMessage(message.Topic, string.Empty, true));
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
                _discoveryQueue?.OfferAsync(new MqttMessage(topic, payload, true));
            }
        }
    }
}
