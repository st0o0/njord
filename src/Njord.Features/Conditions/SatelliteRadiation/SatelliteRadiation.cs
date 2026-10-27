using Microsoft.Extensions.Options;
using Njord.Core.Domain;
using Njord.Core.Solar;
using Njord.Features.Conditions.Shared;
using Njord.Messages.Pipeline;

namespace Njord.Features.Conditions.SatelliteRadiation;

public sealed class SatelliteRadiation(IOptions<SatelliteRadiationConfig> config) : FeatureRule<ForecastSolarInput>
{
    public override FeatureDescriptor Descriptor { get; } = new(
        OutputKind.Condition,
        FeatureCategory.Energy,
        Name: "SatelliteRadiation",
        DefaultEnabled: false,
        new DomainRequirements(
            Satellite: SatelliteParams.GlobalTilted,
            OptionalSatellite: SatelliteParams.GlobalHorizontal));

    public override bool Enabled => config.Value.Enabled;

    protected override ForecastSolarInput? Project(LocationForecast forecast) =>
        forecast is { Forecast: { } w, Satellite: { } s } ? new(w, s) : null;

    protected override PipelineEvent? Evaluate(FeatureContext<ForecastSolarInput> ctx)
    {
        if (SolarIrradianceBlend.Latest(ctx.Current.Satellite, ctx.Now) is not { GTI: { } gti } latest)
        {
            return null;
        }

        var locationForecast = new LocationForecast(ctx.Location, Forecast: ctx.Current.Forecast, Satellite: ctx.Current.Satellite);
        var hours = SolarIrradianceBlend.Hours(locationForecast, ctx.Now, 24);
        double? blended = hours.Count == 0 ? null : Math.Round(hours.Sum(hour => hour.Radiation) / 1000.0, 2);
        return new SatelliteRadiationResult(ctx.Location, latest.ValidFrom, gti, latest.GHI, blended);
    }
}
