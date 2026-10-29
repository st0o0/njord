using System.Collections.Immutable;
using Njord.Core;
using Njord.Core.AirQuality;
using Njord.Core.Forecast;

namespace Njord.Tests.Shared;

public static class AirQualityFixture
{
    public static readonly DateTimeOffset Now = new(2026, 7, 11, 12, 0, 0, TimeSpan.Zero);
    public static readonly Location Lucerne = new("lucerne", 47.05, 8.31);

    public static DomainForecast<AirQualityPoint> Day(Func<int, AirQualityPoint, AirQualityPoint>? shape = null)
    {
        var points = Enumerable.Range(0, 24)
            .Select(hour =>
            {
                var point = new AirQualityPoint(Now.AddHours(hour), 5.0, 10.0, 40.0);
                return shape is null ? point : shape(hour, point);
            })
            .ToImmutableArray();
        return new DomainForecast<AirQualityPoint>(DomainKind.AirQuality, Now, TimeResolution.Hourly, points);
    }
}
