namespace Njord.Configuration;

/// <summary>MQTT broker settings, bound from the <c>Njord:Mqtt</c> section.</summary>
public sealed class MqttOptions
{
    /// <summary>Required — startup validation fails without a broker host.</summary>
    public string Host { get; set; } = string.Empty;

    public int Port { get; set; } = 1883;

    public string? Username { get; set; }

    /// <summary>Never logged. May come from env var <c>Njord__Mqtt__Password</c>.</summary>
    public string? Password { get; set; }

    /// <summary>Home Assistant discovery prefix.</summary>
    public string DiscoveryPrefix { get; set; } = "homeassistant";

    public bool DiscoveryEnabled { get; set; } = true;

    /// <summary>Root of njord's own topics (state, availability).</summary>
    public string BaseTopic { get; set; } = "njord";
}
