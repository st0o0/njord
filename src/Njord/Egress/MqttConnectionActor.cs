using System.Reflection;
using Akka.Actor;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Njord.Configuration;
using Njord.Domain;

namespace Njord.Egress;

/// <summary>
/// Owns the MQTT connection lifecycle: connect/reconnect with backoff, service
/// availability (retained online + Last Will offline), HA birth handling, and
/// the two egress flows — discovery (lifecycle-driven) and telemetry
/// (tick-driven). Retained njord device configs that no longer match the
/// configuration are tombstoned on sight.
/// </summary>
public sealed class MqttConnectionActor : ReceiveActor
{
    private static readonly string Version =
        typeof(MqttConnectionActor).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()
            ?.InformationalVersion ?? "unknown";

    private readonly NjordOptions _options;
    private readonly IMqttPublisher _publisher;
    private readonly ILogger<MqttConnectionActor> _logger;
    private readonly MqttEgressTuning _tuning;
    private readonly IReadOnlyList<int> _horizons;
    private readonly string _availabilityTopic;
    private readonly string _haStatusTopic;
    private readonly string _deviceConfigFilter;
    private readonly HashSet<string> _ownConfigTopics;
    private int _connectAttempts;

    private sealed record Connected;

    private sealed record ConnectFailed(Exception Cause);

    private sealed record Disconnected;

    private sealed record Reconnect;

    private sealed record Inbound(string Topic, string Payload);

    public MqttConnectionActor(
        IOptions<NjordOptions> options,
        IMqttPublisher publisher,
        ILogger<MqttConnectionActor> logger,
        MqttEgressTuning tuning)
    {
        _options = options.Value;
        _publisher = publisher;
        _logger = logger;
        _tuning = tuning;
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
        ReceiveAsync<PublishTelemetry>(OnTelemetryAsync);
    }

    protected override void PreStart() => Connect();

    protected override void PostStop()
    {
        // Clean shutdown bypasses the Last Will — announce offline ourselves.
        try
        {
            _publisher.PublishAsync(_availabilityTopic, "offline", retain: true, CancellationToken.None)
                .Wait(TimeSpan.FromSeconds(2));
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Offline announcement during shutdown failed — the broker may already be gone");
        }
    }

    private void Connect()
    {
        var self = Self;
        _publisher
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
        await GuardedAsync(async () =>
        {
            await _publisher.PublishAsync(_availabilityTopic, "online", retain: true, CancellationToken.None);
            await _publisher.SubscribeAsync(_haStatusTopic, CancellationToken.None);
            await _publisher.SubscribeAsync(_deviceConfigFilter, CancellationToken.None);
            await PublishDiscoveryAsync();
        }, "post-connect announcement and discovery");
    }

    private async Task OnInboundAsync(Inbound message)
    {
        if (message.Topic == _haStatusTopic)
        {
            if (message.Payload == "online")
            {
                _logger.LogInformation("Home Assistant is back online — re-publishing discovery");
                await GuardedAsync(PublishDiscoveryAsync, "re-discovery after HA birth");
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
            await GuardedAsync(
                () => _publisher.PublishAsync(message.Topic, string.Empty, retain: true, CancellationToken.None),
                "tombstone publish");
        }
    }

    private async Task OnTelemetryAsync(PublishTelemetry message)
    {
        foreach (var forecast in message.Forecasts)
        {
            var topic = TopicScheme.StateTopic(_options.Mqtt.BaseTopic, forecast.Location, forecast.Model);
            var payload = StatePayloadBuilder.Build(forecast, _horizons);
            await GuardedAsync(
                () => _publisher.PublishAsync(topic, payload, retain: true, CancellationToken.None),
                $"state publish for {topic}");
        }
    }

    private async Task PublishDiscoveryAsync()
    {
        foreach (var location in _options.Locations)
        {
            foreach (var modelId in _options.Models)
            {
                var model = new WeatherModel(modelId);
                var topic = TopicScheme.ConfigTopic(
                    _options.Mqtt.DiscoveryPrefix, TopicScheme.DeviceId(location.Name, model));
                var payload = DiscoveryPayloadBuilder.Build(
                    location.Name, model, _horizons, _options.Mqtt, _options.PollInterval, Version);
                await _publisher.PublishAsync(topic, payload, retain: true, CancellationToken.None);
            }
        }
    }

    private async Task GuardedAsync(Func<Task> action, string what)
    {
        try
        {
            await action();
        }
        catch (Exception ex)
        {
            // The disconnect callback drives reconnection; a failed publish is
            // logged and retried implicitly on the next cycle/birth.
            _logger.LogWarning(ex, "MQTT egress operation failed: {What}", what);
        }
    }
}
