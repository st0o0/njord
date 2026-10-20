using Njord.Core.Domain;
using Njord.Forecast.AirQuality;
using Njord.Forecast.Flood;
using Njord.Forecast.Marine;
using Njord.Forecast.Weather;

namespace Njord.Tests.Shared;

public static class TestRequirements
{
    public static ResolvedRequirements Resolved => new(All);

    public static DomainRequirements All { get; } = new(
        Enum.GetValues<WeatherParams>().Aggregate(WeatherParams.None, (a, b) => a | b),
        Enum.GetValues<AirQualityParams>().Aggregate(AirQualityParams.None, (a, b) => a | b),
        Enum.GetValues<FloodParams>().Aggregate(FloodParams.None, (a, b) => a | b),
        Enum.GetValues<MarineParams>().Aggregate(MarineParams.None, (a, b) => a | b));
}
