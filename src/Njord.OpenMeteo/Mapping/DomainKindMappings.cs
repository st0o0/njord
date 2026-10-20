using Njord.Forecast;

namespace Njord.OpenMeteo.Mapping;

internal static class DomainKindMappings
{
    public static string ToTelemetryTag(this DomainKind domain) => domain switch
    {
        DomainKind.Weather => "weather",
        DomainKind.AirQuality => "air-quality",
        DomainKind.Flood => "flood",
        DomainKind.Solar => "solar",
        DomainKind.Marine => "marine",
        DomainKind.Seasonal => "seasonal",
        DomainKind.Satellite => "satellite",
        _ => throw new ArgumentOutOfRangeException(nameof(domain), domain, null),
    };
}
