using Njord.Core.Domain;
using Njord.Forecast.Solar;
using Njord.Messages.Pipeline;

namespace Njord.Weather.Lifestyle;

public sealed class SatelliteRadiationHandler : IDomainHandler
{
    public FeatureDescriptor Feature { get; } = new(
        FeatureKind.SatelliteRadiation,
        FeatureCategory.Energy,
        "Satellite-observed solar irradiance blended with the ensemble forecast",
        DefaultEnabled: false,
        new DomainRequirements(
            Satellite: SatelliteParams.GlobalTilted,
            OptionalSatellite: SatelliteParams.GlobalHorizontal));

    public IReadOnlyList<PipelineEvent> Handle(LocationForecast current, LocationForecast? previous, TimeProvider time)
    {
        var now = time.GetUtcNow();
        if (SolarIrradianceBlend.Latest(current.Satellite, now) is not { GTI: { } gti } latest)
        {
            return [];
        }

        var hours = SolarIrradianceBlend.Hours(current, now, 24);
        double? blended = hours.Count == 0 ? null : Math.Round(hours.Sum(hour => hour.Radiation) / 1000.0, 2);
        return [new PipelineEvent.SatelliteRadiationComputed(current.Location, latest.ValidFrom, gti, latest.GHI, blended)];
    }
}
