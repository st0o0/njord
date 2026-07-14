using System.Collections.Concurrent;
using MQTTnet;
using Njord.Configuration;

namespace Njord.Tests.Shared;

public static class MosquittoHelper
{
    public const string MosquittoConf = "listener 1883\nallow_anonymous true\n";

    public static async Task<IReadOnlyDictionary<string, string>> CollectRetainedAsync(
        MqttOptions options, string[] filters, CancellationToken ct)
    {
        var seen = new ConcurrentDictionary<string, string>();
        using var client = new MqttClientFactory().CreateMqttClient();
        client.ApplicationMessageReceivedAsync += e =>
        {
            seen[e.ApplicationMessage.Topic] = e.ApplicationMessage.ConvertPayloadToString() ?? string.Empty;
            return Task.CompletedTask;
        };
        await client.ConnectAsync(
            new MqttClientOptionsBuilder().WithTcpServer(options.Host, options.Port).Build(), ct);
        foreach (var filter in filters)
        {
            await client.SubscribeAsync(filter, cancellationToken: ct);
        }

        await Task.Delay(TimeSpan.FromSeconds(3), ct);
        await client.DisconnectAsync(cancellationToken: ct);
        return seen;
    }
}
