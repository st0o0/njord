using Microsoft.Extensions.Options;
using Njord.Core.Domain;
using Njord.Core.Forecast;
using Njord.Messages.Results;
using Njord.Messages.Results.Conditions;
using Njord.Messages.Stream;

namespace Njord.Features.Conditions.Gardening;

public sealed class Gardening(IOptions<GardeningConfig> config) : FeatureRule<ForecastInput>
{

    public override FeatureDescriptor Descriptor { get; } = new(
        OutputKind.Condition,
        FeatureCategory.Lifestyle,
        Name: "Gardening",
        new DomainRequirements(Forecast: ForecastParams.Temperature | ForecastParams.WindSpeed | ForecastParams.Precipitation),
        EntityMetadata: new EntityMetadata(EntityPlatform.BinarySensor));

    public override bool Enabled => config.Value.Enabled;

    protected override ForecastInput? Project(LocationForecast forecast) =>
        forecast.Forecast is { } w ? new ForecastInput(w) : null;

    protected override PipelineEvent? Evaluate(FeatureContext<ForecastInput> ctx)
    {
        var day = ctx.Current.Forecast.NextHours(ctx.Now);
        var twoDays = ctx.Current.Forecast.NextHours(ctx.Now, 48);
        if (day.Count == 0)
        {
            return null;
        }

        var cfg = config.Value;
        var frostRisk = day.Min(p => p.Temperature) < cfg.FrostTemperatureCelsius;
        var frostWithinTwoDays = twoDays.Min(p => p.Temperature) < cfg.FrostTemperatureCelsius;
        var windy = day.Max(p => p.WindSpeed) >= cfg.WindLimitMs;
        var plantingSafe = !frostWithinTwoDays && !windy;
        var wateringNeeded = day.Sum(p => p.Precipitation) < cfg.RainThresholdMmH;
        return new GardeningResult(ctx.Location, plantingSafe ? GardeningAdvice.Plant : GardeningAdvice.Hold, plantingSafe, wateringNeeded, frostRisk);
    }
}
