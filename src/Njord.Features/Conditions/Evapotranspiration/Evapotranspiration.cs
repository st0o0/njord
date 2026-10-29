using Microsoft.Extensions.Options;
using Njord.Core.Domain;
using Njord.Core.Forecast;
using Njord.Messages.Pipeline;

namespace Njord.Features.Conditions.Evapotranspiration;

public sealed class Evapotranspiration(IOptions<EvapotranspirationConfig> config) : FeatureRule<ForecastInput>
{
    public override FeatureDescriptor Descriptor { get; } = new(
        OutputKind.Condition,
        FeatureCategory.Lifestyle,
        Name: "Evapotranspiration",
        DefaultEnabled: false,
        new DomainRequirements(Forecast: ForecastParams.Evapotranspiration));

    public override bool Enabled => config.Value.Enabled;

    protected override ForecastInput? Project(LocationForecast forecast) =>
        forecast.Forecast is { } w ? new(w) : null;

    protected override PipelineEvent? Evaluate(FeatureContext<ForecastInput> ctx)
    {
        var today = ctx.Current.Forecast.NextHours(ctx.Now, 24);
        var dailySum = 0.0;
        foreach (var point in today)
        {
            if (point.Evapotranspiration is { } et)
            {
                dailySum += et;
            }
        }

        if (dailySum <= 0)
        {
            return null;
        }

        var rounded = Math.Round(dailySum, 1);
        var cfg = config.Value;
        var level = rounded switch
        {
            _ when rounded >= cfg.HighMm => EvapotranspirationLevel.High,
            _ when rounded >= cfg.LowMm => EvapotranspirationLevel.Moderate,
            _ => EvapotranspirationLevel.Low,
        };

        return new EvapotranspirationResult(ctx.Location, level, rounded);
    }
}
