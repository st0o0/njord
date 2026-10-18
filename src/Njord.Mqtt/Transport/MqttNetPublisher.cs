using Njord.Mqtt.Topics;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MQTTnet;
using MQTTnet.Protocol;
using Njord.Core.Configuration.Mqtt;

namespace Njord.Mqtt.Transport;

public sealed class MqttNetPublisher(IOptions<MqttOptions> mqttOptions, ILogger<MqttNetPublisher> logger)
    : IMqttConnection, IMqttTransport, IDisposable
{
    private readonly MqttOptions _options = mqttOptions.Value;
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
            _client.DisconnectedAsync += e =>
            {
                if (IsLostConnection(e))
                {
                    onDisconnected();
                }

                return Task.CompletedTask;
            };
            _handlersAttached = true;
        }

        var result = await _client.ConnectAsync(BuildClientOptions(_options), cancellationToken);
        EnsureAccepted(result);
    }

    internal static MqttClientOptions BuildClientOptions(MqttOptions options)
    {
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

        return builder.Build();
    }

    internal static void EnsureAccepted(MqttClientConnectResult result)
    {
        if (result.ResultCode != MqttClientConnectResultCode.Success)
        {
            throw new InvalidOperationException(
                $"MQTT broker refused the connection: {result.ResultCode} ({result.ReasonString})");
        }
    }

    internal static bool IsLostConnection(MqttClientDisconnectedEventArgs args) => args.ClientWasConnected;

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
            logger.LogDebug(ex, "Clean MQTT disconnect failed: the Last Will covers this path");
        }

        _client.Dispose();
    }

    public void Dispose() { }
}
