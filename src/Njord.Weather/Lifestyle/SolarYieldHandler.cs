using Njord.Core.Domain;
using Njord.Forecast.Weather;
using Njord.Messages.Pipeline;

namespace Njord.Weather.Lifestyle;

public sealed class SolarYieldHandler : IDomainHandler
{
    public const double SystemEfficiency = 0.8;

    public FeatureDescriptor Feature { get; } = new(
        FeatureKind.SolarYield,
        FeatureCategory.Energy,
        "Expected solar yield from tilted irradiance",
        DefaultEnabled: false,
        new DomainRequirements(Weather: WeatherParams.GlobalTiltedIrradiance));

    public IReadOnlyList<PipelineEvent> Handle(LocationForecast current, LocationForecast? previous, TimeProvider time)
    {
        if (current.Weather is not { } weather)
        {
            return [];
        }

        var hours = ForecastQueries.Upcoming(weather, time.GetUtcNow(), 24)
            .Where(point => point.GlobalTiltedIrradiance is not null)
            .Select(point => (point.ValidFrom, Radiation: point.GlobalTiltedIrradiance!.Value))
            .ToList();
        if (hours.Count == 0)
        {
            return [];
        }

        var kwhPerSquareMeter = hours.Sum(hour => hour.Radiation) / 1000.0;
        var yield = kwhPerSquareMeter * SystemEfficiency;
        var peak = hours.MaxBy(hour => hour.Radiation);
        return [new PipelineEvent.SolarYieldComputed(current.Location, Math.Round(yield, 2), peak.ValidFrom.Hour)];
    }
}
