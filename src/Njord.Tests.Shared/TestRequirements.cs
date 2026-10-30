using Njord.Core;
using Njord.Core.AirQuality;
using Njord.Core.Domain;
using Njord.Core.Flood;
using Njord.Core.Forecast;
using Njord.Core.Marine;
using Njord.Core.Seasonal;

namespace Njord.Tests.Shared;

public static class TestRequirements
{
    public static DomainRequirements All { get; } = new(
        Forecast: Enum.GetValues<ForecastParams>().Aggregate(ForecastParams.None, (a, b) => a | b),
        AirQuality: Enum.GetValues<AirQualityParams>().Aggregate(AirQualityParams.None, (a, b) => a | b),
        Flood: Enum.GetValues<FloodParams>().Aggregate(FloodParams.None, (a, b) => a | b),
        Marine: Enum.GetValues<MarineParams>().Aggregate(MarineParams.None, (a, b) => a | b),
        Seasonal: Enum.GetValues<SeasonalParams>().Aggregate(SeasonalParams.None, (a, b) => a | b));

    public static DomainRequirements For(params IEnumerable<DomainKind> domains) =>
        domains.Aggregate(new DomainRequirements(), (all, domain) => all | domain switch
        {
            DomainKind.Forecast => new DomainRequirements(Forecast: ForecastParams.Temperature),
            DomainKind.AirQuality => new DomainRequirements(AirQuality: AirQualityParams.Pm25),
            DomainKind.Flood => new DomainRequirements(Flood: FloodParams.Discharge),
            DomainKind.Marine => new DomainRequirements(Marine: MarineParams.WaveHeight),
            DomainKind.Seasonal => new DomainRequirements(Seasonal: SeasonalParams.TemperatureAnomaly),
            DomainKind.Ensemble => new DomainRequirements(Ensemble: EnsembleParams.TemperatureSpread),
            _ => throw new ArgumentOutOfRangeException(nameof(domain)),
        });
}
