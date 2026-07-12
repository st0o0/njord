namespace Njord.Egress;

public interface IMqttTransport
{
    Task SendAsync(string topic, string payload, bool retain, CancellationToken cancellationToken);
}
