using System.Text;
using Njord.Domain;

namespace Njord.Egress;

/// <summary>All MQTT topic and identifier construction lives here — one place, one shape.</summary>
public static class TopicScheme
{
    public static string DeviceId(string location, WeatherModel model)
        => $"njord_{Slug(location)}_{model.Id}";

    public static string ConfigTopic(string discoveryPrefix, string deviceId)
        => $"{discoveryPrefix}/device/{deviceId}/config";

    public static string StateTopic(string baseTopic, string location, WeatherModel model)
        => $"{baseTopic}/{Slug(location)}/{model.Id}/state";

    public static string AvailabilityTopic(string baseTopic)
        => $"{baseTopic}/status";

    public static string UniqueId(string location, WeatherModel model, WeatherParameter parameter, int horizonHours)
        => $"{DeviceId(location, model)}_{parameter.JsonKey()}_h{horizonHours}";

    /// <summary>Lowercases and maps everything outside [a-z0-9] to '_' — topic- and entity-id-safe.</summary>
    public static string Slug(string value)
    {
        var builder = new StringBuilder(value.Length);
        foreach (var c in value.ToLowerInvariant())
        {
            builder.Append(c is >= 'a' and <= 'z' or >= '0' and <= '9' ? c : '_');
        }

        return builder.ToString();
    }
}
