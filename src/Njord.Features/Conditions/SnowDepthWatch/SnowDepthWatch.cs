using Microsoft.Extensions.Options;
using Njord.Core.Domain;
using Njord.Core.Forecast;
using Njord.Features.Alerts.Shared;
using Njord.Messages.Results;
using Njord.Messages.Results.Conditions;
using Njord.Messages.Stream;

namespace Njord.Features.Conditions.SnowDepthWatch;

public sealed class SnowDepthWatch(IOptions<SnowDepthWatchConfig> config) : FeatureRule<ForecastInput>
{
    public override FeatureDescriptor Descriptor { get; } = new(
        OutputKind.Condition,
        FeatureCategory.Forecast,
        Name: "SnowDepthWatch",
        new DomainRequirements(Forecast: ForecastParams.SnowDepth));

    public override bool Enabled => config.Value.Enabled;

    protected override ForecastInput? Project(LocationForecast forecast) =>
        forecast.Forecast is { } w ? new(w) : null;

    protected override PipelineEvent? Evaluate(FeatureContext<ForecastInput> ctx)
    {
        var window = ctx.Current.Forecast.NextHoursInclusive(ctx.Now);
        if (window.HighestBy(p => p.SnowDepth) is not { SnowDepth: { } maxDepth })
        {
            return null;
        }

        var cfg = config.Value;
        var depthCm = Math.Round(maxDepth * 100, 1);
        var level = depthCm switch
        {
            _ when depthCm >= cfg.ExtremeThreshold => SnowDepthLevel.Extreme,
            _ when depthCm >= cfg.HeavyThreshold => SnowDepthLevel.Heavy,
            _ when depthCm >= cfg.ModerateThreshold => SnowDepthLevel.Moderate,
            _ when depthCm >= cfg.LightThreshold => SnowDepthLevel.Light,
            _ => SnowDepthLevel.None,
        };

        if (level == SnowDepthLevel.None)
        {
            return null;
        }

        return new SnowDepthWatchResult(ctx.Location, level, depthCm);
    }
}
