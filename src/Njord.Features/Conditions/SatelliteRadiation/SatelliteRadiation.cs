using Microsoft.Extensions.Options;
using Njord.Core.Domain;
using Njord.Features.Conditions.Shared;
using Njord.Forecast.Solar;
using Njord.Messages.Pipeline;

namespace Njord.Features.Conditions.SatelliteRadiation;

public sealed class SatelliteRadiation(IOptions<SatelliteRadiationConfig> config) : FeatureRule<WeatherSolarInput>
{
    public override FeatureDescriptor Descriptor { get; } = new(
        OutputKind.Condition,
        FeatureCategory.Energy,
        DefaultEnabled: false,
        new DomainRequirements(
            Satellite: SatelliteParams.GlobalTilted,
            OptionalSatellite: SatelliteParams.GlobalHorizontal));

    public override bool Enabled => config.Value.Enabled;

    protected override WeatherSolarInput? Project(LocationForecast forecast) =>
        forecast is { Weather: { } w, Satellite: { } s } ? new(w, s) : null;

    protected override PipelineEvent? Evaluate(FeatureContext<WeatherSolarInput> ctx)
    {
        if (SolarIrradianceBlend.Latest(ctx.Current.Satellite, ctx.Now) is not { GTI: { } gti } latest)
        {
            return null;
        }

        var locationForecast = new LocationForecast(ctx.Location, Weather: ctx.Current.Weather, Satellite: ctx.Current.Satellite);
        var hours = SolarIrradianceBlend.Hours(locationForecast, ctx.Now, 24);
        double? blended = hours.Count == 0 ? null : Math.Round(hours.Sum(hour => hour.Radiation) / 1000.0, 2);
        return new SatelliteRadiationResult(ctx.Location, FeatureData.Number(gti), Fields(latest.ValidFrom, gti, latest.GHI, blended));
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
