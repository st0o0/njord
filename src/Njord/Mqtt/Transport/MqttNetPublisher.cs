using MQTTnet;
using MQTTnet.Protocol;
using Njord.Configuration;

namespace Njord.Mqtt.Transport;

/// <summary>MQTTnet-backed publisher. Registers the Last Will as part of connecting.</summary>
public sealed class MqttNetPublisher(MqttOptions options, ILogger<MqttNetPublisher> logger)
    : IMqttConnection, IMqttTransport
{
    private readonly IMqttClient _client = new MqttClientFactory().CreateMqttClient();
    private bool _handlersAttached;

    public async Task ConnectAsync(Action<string, string> onMessage, Action onDisconnected, CancellationToken cancellationToken)
    {
        if (!_handlersAttached)
        {
            _client.ApplicationMessageReceivedAsync += e =>
            {
                onMessage(e.ApplicationMessage.Topic, e.ApplicationMessage.ConvertPayloadToString() ?? string.Empty);
                return Task.CompletedTask;
            };
            _client.DisconnectedAsync += _ =>
            {
                onDisconnected();
                return Task.CompletedTask;
            };
            _handlersAttached = true;
        }

        var builder = new MqttClientOptionsBuilder()
            .WithTcpServer(options.Host, options.Port)
            .WithWillTopic(TopicScheme.AvailabilityTopic(options.BaseTopic))
            .WithWillPayload("offline")
            .WithWillRetain()
            .WithWillQualityOfServiceLevel(MqttQualityOfServiceLevel.AtLeastOnce);
        if (!string.IsNullOrWhiteSpace(options.Username))
        {
            builder = builder.WithCredentials(options.Username, options.Password);
        }

        await _client.ConnectAsync(builder.Build(), cancellationToken);
    }

    public Task SendAsync(string topic, string payload, bool retain, CancellationToken cancellationToken)
        => _client.PublishAsync(
            new MqttApplicationMessageBuilder()
                .WithTopic(topic)
                .WithPayload(payload)
                .WithRetainFlag(retain)
                .WithQualityOfServiceLevel(MqttQualityOfServiceLevel.AtLeastOnce)
                .Build(),
            cancellationToken);

    public Task SubscribeAsync(string topicFilter, CancellationToken cancellationToken)
        => _client.SubscribeAsync(topicFilter, MqttQualityOfServiceLevel.AtLeastOnce, cancellationToken);

    public async ValueTask DisposeAsync()
    {
        try
        {
            await _client.DisconnectAsync();
        }
        catch (Exception ex)
        {
            logger.LogDebug(ex, "Clean MQTT disconnect failed — the Last Will covers this path");
        }

        _client.Dispose();
    }
}
