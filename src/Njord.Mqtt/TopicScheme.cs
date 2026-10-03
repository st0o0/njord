using Njord.Domain.Weather;
using Njord.Egress;

namespace Njord.Mqtt;

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

    public static string EnrichmentDeviceId(string location, string typeName)
        => $"njord_{Slug(location)}_{typeName}";

    public static string EnrichmentTopic(string baseTopic, string location, string typeName)
        => $"{baseTopic}/{Slug(location)}/{typeName}";

    public static string EnrichmentSubTopic(string baseTopic, string location, string typeName, string sub)
        => $"{baseTopic}/{Slug(location)}/{typeName}/{sub}";

    public static string Slug(string value)
        => TopicSlug.Slug(value);
}
