namespace Njord.Mqtt;

public interface IEnrichmentPresenter
{
    string TypeName { get; }
    bool Enabled { get; }
    string DeviceId(string location);
    string BuildDiscoveryPayload(DiscoveryContext ctx, string location);
    IReadOnlyList<MqttMessage> ToStateMessages(object result, string baseTopic, string location);
}
