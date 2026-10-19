using Njord.Model;

namespace Njord.Mqtt.Topics;

public static class DomainKindTopicSegments
{
    public static string ToTopicSegment(this DomainKind domain) => domain switch
    {
        DomainKind.Weather => "weather",
        DomainKind.AirQuality => "air-quality",
        DomainKind.Flood => "flood",
        DomainKind.Solar => "solar",
        DomainKind.Marine => "marine",
        _ => throw new ArgumentOutOfRangeException(nameof(domain), domain, null),
    };
}
