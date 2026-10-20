using Microsoft.Extensions.Options;
using Njord.Core.Domain;
using Njord.Features.Conditions.Shared;
using Njord.Forecast.Weather;
using Njord.Messages.Pipeline;

namespace Njord.Features.Conditions.SolarYield;

public sealed class SolarYield(IOptions<SolarYieldConfig> config) : FeatureRule<WeatherSolarInput>
{
    public const double SystemEfficiency = 0.8;

    public override FeatureDescriptor Descriptor { get; } = new(
        OutputKind.Condition,
        FeatureCategory.Energy,
        Name: "SolarYield",
        DefaultEnabled: false,
        new DomainRequirements(Weather: WeatherParams.GlobalTiltedIrradiance));

    public override bool Enabled => config.Value.Enabled;

    protected override WeatherSolarInput? Project(LocationForecast forecast) =>
        forecast.Weather is { } w ? new(w, forecast.Satellite!) : null;

    protected override PipelineEvent? Evaluate(FeatureContext<WeatherSolarInput> ctx)
    {
        var locationForecast = new LocationForecast(ctx.Location, Weather: ctx.Current.Weather, Satellite: ctx.Current.Satellite);
        var hours = SolarIrradianceBlend.Hours(locationForecast, ctx.Now, 24);
        if (hours.Count == 0)
        {
            return null;
        }

        var kwhPerSquareMeter = hours.Sum(hour => hour.Radiation) / 1000.0;
        var yield = kwhPerSquareMeter * SystemEfficiency;
        var peak = hours.MaxBy(hour => hour.Radiation);
        return new SolarYieldResult(ctx.Location, Math.Round(yield, 2), peak.ValidFrom.Hour);
    }
}
