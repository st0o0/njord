using Microsoft.Extensions.Options;
using Njord.Core.Domain;
using Njord.Features.Alerts.Shared;
using Njord.Forecast.Weather;
using Njord.Messages.Pipeline;

namespace Njord.Features.Conditions.SnowDepthWatch;

public sealed class SnowDepthWatch(IOptions<SnowDepthWatchConfig> config) : FeatureRule<WeatherInput>
{
    public override FeatureDescriptor Descriptor { get; } = new(
        OutputKind.Condition,
        FeatureCategory.Weather,
        Name: "SnowDepthWatch",
        DefaultEnabled: false,
        new DomainRequirements(Weather: WeatherParams.SnowDepth));

    public override bool Enabled => config.Value.Enabled;

    protected override WeatherInput? Project(LocationForecast forecast) =>
        forecast.Weather is { } w ? new(w) : null;

    protected override PipelineEvent? Evaluate(FeatureContext<WeatherInput> ctx)
    {
        var window = ctx.Current.Weather.NextHoursInclusive(ctx.Now);
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
