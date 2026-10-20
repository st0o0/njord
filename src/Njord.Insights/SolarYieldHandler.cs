using Njord.Core.Domain;
using Njord.Forecast.Weather;
using Njord.Messages.Pipeline;

namespace Njord.Insights;

public sealed class SolarYieldHandler : IDomainHandler
{
    public const double SystemEfficiency = 0.8;

    public string Name => "SolarYield";

    public FeatureDescriptor Feature { get; } = new(
        OutputKind.Condition,
        FeatureCategory.Energy,
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
        return [new ConditionResult(current.Location, this.WireKey(), FeatureData.Number(Math.Round(yield, 2)), FeatureData.Of(
            ("kwh_per_kwp", FeatureData.Number(Math.Round(yield, 2))),
            ("peak_hour", FeatureData.Number(peak.ValidFrom.Hour))))];
    }
}
