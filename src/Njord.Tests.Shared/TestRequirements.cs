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
        Enum.GetValues<WeatherParam>().Aggregate(WeatherParam.None, (a, b) => a | b),
        Enum.GetValues<AirQualityParam>().Aggregate(AirQualityParam.None, (a, b) => a | b),
        Enum.GetValues<FloodParam>().Aggregate(FloodParam.None, (a, b) => a | b),
        Enum.GetValues<MarineParam>().Aggregate(MarineParam.None, (a, b) => a | b));
}
