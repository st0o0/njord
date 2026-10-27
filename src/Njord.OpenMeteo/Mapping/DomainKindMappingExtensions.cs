using Njord.Core;

namespace Njord.OpenMeteo.Mapping;

internal static class DomainKindMappingExtensions
{
    public static string ToTelemetryTag(this DomainKind domain) => domain switch
    {
        DomainKind.Forecast => "weather",
        DomainKind.AirQuality => "air-quality",
        DomainKind.Flood => "flood",
        DomainKind.Marine => "marine",
        DomainKind.Seasonal => "seasonal",
        DomainKind.Satellite => "satellite",
        DomainKind.Ensemble => "ensemble",
        _ => throw new ArgumentOutOfRangeException(nameof(domain), domain, null),
    };
}
