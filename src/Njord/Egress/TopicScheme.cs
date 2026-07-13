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

    public static string AlertDeviceId(string location)
        => $"njord_{Slug(location)}_alerts";

    public static string AlertTopic(string baseTopic, string location, string alertType)
        => $"{baseTopic}/{Slug(location)}/alerts/{alertType}";

    public static string ConsensusDeviceId(string location)
        => $"njord_{Slug(location)}_consensus";

    public static string ConsensusHorizonTopic(string baseTopic, string location, string horizon)
        => $"{baseTopic}/{Slug(location)}/consensus/{horizon}";

    public static string DerivedDeviceId(string location)
        => $"njord_{Slug(location)}_derived";

    public static string DerivedHorizonTopic(string baseTopic, string location, string horizon)
        => $"{baseTopic}/{Slug(location)}/derived/{horizon}";

    public static string DerivedMetaTopic(string baseTopic, string location)
        => $"{baseTopic}/{Slug(location)}/derived/meta";

    public static string TrendDeviceId(string location)
        => $"njord_{Slug(location)}_trends";

    public static string TrendTopic(string baseTopic, string location)
        => $"{baseTopic}/{Slug(location)}/trends";

    public static string IndexDeviceId(string location)
        => $"njord_{Slug(location)}_indices";

    public static string IndexTopic(string baseTopic, string location)
        => $"{baseTopic}/{Slug(location)}/indices";

    public static string EnergyDeviceId(string location)
        => $"njord_{Slug(location)}_energy";

    public static string EnergyTopic(string baseTopic, string location)
        => $"{baseTopic}/{Slug(location)}/energy";

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
