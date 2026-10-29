using Microsoft.Extensions.Options;
using Njord.Core.Configuration;
using Njord.Core.Domain;
using Njord.Core.Forecast;
using Njord.Features.Conditions.Shared;
using Njord.Messages.Results;
using Njord.Messages.Results.Conditions;
using Njord.Messages.Stream;

namespace Njord.Features.Conditions.SolarYield;

public sealed class SolarYield(IOptions<SolarYieldConfig> config, IOptions<SolarYieldOptions> solar) : FeatureRule<ForecastSolarInput>
{
    public override FeatureDescriptor Descriptor { get; } = new(
        OutputKind.Condition,
        FeatureCategory.Energy,
        Name: "SolarYield",
        new DomainRequirements(Forecast: ForecastParams.GlobalTiltedIrradiance));

    public override bool Enabled => config.Value.Enabled;

    protected override ForecastSolarInput? Project(LocationForecast forecast) =>
        forecast.Forecast is { } w ? new(w, forecast.Satellite!) : null;

    protected override PipelineEvent? Evaluate(FeatureContext<ForecastSolarInput> ctx)
    {
        var locationForecast = new LocationForecast(ctx.Location, Forecast: ctx.Current.Forecast, Satellite: ctx.Current.Satellite);
        var hours = SolarIrradianceBlend.Hours(locationForecast, ctx.Now, 24);
        if (hours.Count == 0)
        {
            return null;
        }

        var kwhPerSquareMeter = hours.Sum(hour => hour.Radiation) / 1000.0;
        var yield = kwhPerSquareMeter * solar.Value.SystemEfficiency * solar.Value.PeakKw;
        var peak = hours.MaxBy(hour => hour.Radiation);
        return new SolarYieldResult(ctx.Location, Math.Round(yield, 2), peak.ValidFrom.Hour);
    }
}
