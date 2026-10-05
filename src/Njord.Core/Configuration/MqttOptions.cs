namespace Njord.Configuration;

public sealed class MqttOptions
{
    public const string SectionName = "Njord:Mqtt";

    public bool Enabled { get; set; }

    public string Host { get; set; } = string.Empty;

    public int Port { get; set; } = 1883;

    public string? Username { get; set; }

    public string? Password { get; set; }

    public string DiscoveryPrefix { get; set; } = "homeassistant";

    public bool DiscoveryEnabled { get; set; } = true;

    public string BaseTopic { get; set; } = "njord";
}
