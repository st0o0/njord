using System.Collections.Concurrent;
using MQTTnet;

namespace Njord.IntegrationTests.Broker;

public sealed record ObservedMessage(string Topic, string Payload, bool Retain);

// A plain MQTTnet client that subscribes to a filter and records everything it receives.
public sealed class MqttObserver : IAsyncDisposable
{
    private readonly IMqttClient _client = new MqttClientFactory().CreateMqttClient();
    private readonly ConcurrentQueue<ObservedMessage> _messages = new();

    public IReadOnlyList<ObservedMessage> Messages => [.. _messages];

    public static async Task<MqttObserver> SubscribeAsync(int port, string filter, CancellationToken cancellationToken)
    {
        var observer = new MqttObserver();
        observer._client.ApplicationMessageReceivedAsync += e =>
        {
            observer._messages.Enqueue(new ObservedMessage(
                e.ApplicationMessage.Topic, e.ApplicationMessage.ConvertPayloadToString() ?? string.Empty, e.ApplicationMessage.Retain));
            return Task.CompletedTask;
        };
        await observer._client.ConnectAsync(new MqttClientOptionsBuilder().WithTcpServer("127.0.0.1", port).Build(), cancellationToken);
        await observer._client.SubscribeAsync(filter, MQTTnet.Protocol.MqttQualityOfServiceLevel.AtLeastOnce, cancellationToken);
        return observer;
    }

    public async ValueTask DisposeAsync()
    {
        try
        {
            await _client.DisconnectAsync();
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // The broker may be gone already.
        }

        _client.Dispose();
    }
}
