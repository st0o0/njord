using System.Text;
using Njord.Domain;

namespace Njord.Egress;

public static class TopicScheme
{
    public static string DeviceId(string location, WeatherModel model)
        => $"njord_{Slug(location)}_{model.Id}";

    public static string ConfigTopic(string discoveryPrefix, string deviceId)
        => $"{discoveryPrefix}/device/{deviceId}/config";

    public static string HorizonTopic(string baseTopic, string location, WeatherModel model, string horizon)
        => $"{baseTopic}/{Slug(location)}/{model.Id}/{horizon}";


    public static string AvailabilityTopic(string baseTopic)
        => $"{baseTopic}/status";

    public static string HourlyUniqueId(string location, WeatherModel model, ParameterDef parameter, int horizonHours)
        => $"{DeviceId(location, model)}_{parameter.JsonKey}_h{horizonHours}";

    public static string DailyUniqueId(string location, WeatherModel model, ParameterDef parameter, int dayOffset)
        => $"{DeviceId(location, model)}_{parameter.JsonKey}_d{dayOffset}";

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
