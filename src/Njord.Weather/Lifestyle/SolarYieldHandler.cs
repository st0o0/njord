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

    // Observed hours come from the satellite when the SatelliteRadiation feature delivers them, later hours from the ensemble.
    public IReadOnlyList<PipelineEvent> Handle(LocationForecast current, LocationForecast? previous, TimeProvider time)
    {
        var hours = SolarIrradianceBlend.Hours(current, time.GetUtcNow(), 24);
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
