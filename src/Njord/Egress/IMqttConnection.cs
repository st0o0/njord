namespace Njord.Egress;

public interface IMqttConnection : IAsyncDisposable
{
    Task ConnectAsync(Action<string, string> onMessage, Action onDisconnected, CancellationToken cancellationToken);

    Task SubscribeAsync(string topicFilter, CancellationToken cancellationToken);
}
