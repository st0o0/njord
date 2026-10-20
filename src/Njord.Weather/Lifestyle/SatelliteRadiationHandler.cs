using Njord.Core.Domain;
using Njord.Forecast.Solar;
using Njord.Messages.Pipeline;

namespace Njord.Weather.Lifestyle;

public sealed class SatelliteRadiationHandler : IDomainHandler
{
    public string Name => "SatelliteRadiation";

    public FeatureDescriptor Feature { get; } = new(
        OutputKind.Condition,
        FeatureCategory.Energy,
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
        return [new ConditionResult(current.Location, this.WireKey(), FeatureData.Number(gti), Fields(latest.ValidFrom, gti, latest.GHI, blended))];
    }

    private static IReadOnlyDictionary<string, string> Fields(DateTimeOffset observedAt, double gti, double? ghi, double? blended)
    {
        var fields = new Dictionary<string, string>
        {
            ["observed_at"] = FeatureData.Time(observedAt),
            ["gti"] = FeatureData.Number(gti),
        };

        if (ghi is { } horizontal)
        {
            fields["ghi"] = FeatureData.Number(horizontal);
        }

        if (blended is { } kwh)
        {
            fields["blended_kwh_per_m2"] = FeatureData.Number(kwh);
        }

        return fields;
    }
}
