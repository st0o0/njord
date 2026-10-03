using Njord.Configuration;

namespace Njord.Mqtt;

public sealed record DiscoveryContext(
    MqttOptions Mqtt,
    TimeSpan PollInterval,
    string Version);
