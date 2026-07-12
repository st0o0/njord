namespace Njord.Egress;

/// <summary>
/// Thin seam over the MQTT client so the connection actor stays unit-testable.
/// The implementation configures the Last Will (retained <c>offline</c> on the
/// service availability topic) as part of connecting.
/// </summary>
public interface IMqttPublisher : IAsyncDisposable
{
    Task ConnectAsync(Action<string, string> onMessage, Action onDisconnected, CancellationToken cancellationToken);

    Task PublishAsync(string topic, string payload, bool retain, CancellationToken cancellationToken);

    Task SubscribeAsync(string topicFilter, CancellationToken cancellationToken);
}
