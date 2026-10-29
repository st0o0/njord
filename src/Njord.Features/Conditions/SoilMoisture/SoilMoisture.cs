using Microsoft.Extensions.Options;
using Njord.Core.Domain;
using Njord.Core.Forecast;
using Njord.Features.Alerts.Shared;
using Njord.Messages.Pipeline;
using Njord.Messages.Results;
using Njord.Messages.Stream;

namespace Njord.Features.Conditions.SoilMoisture;

public sealed class SoilMoisture(IOptions<SoilMoistureConfig> config) : FeatureRule<ForecastInput>
{
    public override FeatureDescriptor Descriptor { get; } = new(
        OutputKind.Condition,
        FeatureCategory.Lifestyle,
        Name: "SoilMoisture",
        DefaultEnabled: false,
        new DomainRequirements(Forecast: ForecastParams.SoilMoisture0To1cm));

    public override bool Enabled => config.Value.Enabled;

    protected override ForecastInput? Project(LocationForecast forecast) =>
        forecast.Forecast is { } w ? new(w) : null;

    protected override PipelineEvent? Evaluate(FeatureContext<ForecastInput> ctx)
    {
        var window = ctx.Current.Forecast.NextHoursInclusive(ctx.Now);
        if (window.LowestBy(p => p.SoilMoisture0To1cm) is not { SoilMoisture0To1cm: { } moisture })
        {
            return null;
        }

        var cfg = config.Value;
        var level = moisture switch
        {
            _ when moisture < cfg.DryThreshold => SoilMoistureLevel.Dry,
            _ when moisture > cfg.WetThreshold => SoilMoistureLevel.Wet,
            _ => SoilMoistureLevel.Optimal,
        };

        return new SoilMoistureResult(ctx.Location, level, Math.Round(moisture, 3));
    }
}
