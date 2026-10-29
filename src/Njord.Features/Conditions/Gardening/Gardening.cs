using Microsoft.Extensions.Options;
using Njord.Core.Domain;
using Njord.Core.Forecast;
using Njord.Messages.Results;
using Njord.Messages.Results.Conditions;
using Njord.Messages.Stream;

namespace Njord.Features.Conditions.Gardening;

public sealed class Gardening(IOptions<GardeningConfig> config) : FeatureRule<ForecastInput>
{
    public const double FrostTemperature = 2.0;
    public const double RainThreshold = 2.0;
    public const double WindLimit = 10.0;

    public override FeatureDescriptor Descriptor { get; } = new(
        OutputKind.Condition,
        FeatureCategory.Lifestyle,
        Name: "Gardening",
        new DomainRequirements(Forecast: ForecastParams.Temperature | ForecastParams.WindSpeed | ForecastParams.Precipitation));

    public override bool Enabled => config.Value.Enabled;

    protected override ForecastInput? Project(LocationForecast forecast) =>
        forecast.Forecast is { } w ? new(w) : null;

    protected override PipelineEvent? Evaluate(FeatureContext<ForecastInput> ctx)
    {
        var day = ctx.Current.Forecast.NextHours(ctx.Now);
        var twoDays = ctx.Current.Forecast.NextHours(ctx.Now, 48);
        if (day.Count == 0)
        {
            return null;
        }

        var frostRisk = day.Min(p => p.Temperature) < FrostTemperature;
        var frostWithinTwoDays = twoDays.Min(p => p.Temperature) < FrostTemperature;
        var windy = day.Max(p => p.WindSpeed) >= WindLimit;
        var plantingSafe = !frostWithinTwoDays && !windy;
        var wateringNeeded = day.Sum(p => p.Precipitation) < RainThreshold;
        return new GardeningResult(ctx.Location, plantingSafe ? GardeningAdvice.Plant : GardeningAdvice.Hold, plantingSafe, wateringNeeded, frostRisk);
    }
}
