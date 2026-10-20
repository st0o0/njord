using Microsoft.Extensions.Options;
using Njord.Core.Domain;
using Njord.Forecast.Weather;
using Njord.Messages.Pipeline;

namespace Njord.Features.Conditions.Gardening;

public sealed class Gardening(IOptions<GardeningConfig> config) : FeatureRule<WeatherInput>
{
    public const double FrostTemperature = 2.0;
    public const double RainThreshold = 2.0;
    public const double WindLimit = 10.0;

    public override FeatureDescriptor Descriptor { get; } = new(
        OutputKind.Condition,
        FeatureCategory.Lifestyle,
        Name: "Gardening",
        DefaultEnabled: false,
        new DomainRequirements(Weather: WeatherParams.Temperature | WeatherParams.WindSpeed | WeatherParams.Precipitation));

    public override bool Enabled => config.Value.Enabled;

    protected override WeatherInput? Project(LocationForecast forecast) =>
        forecast.Weather is { } w ? new(w) : null;

    protected override PipelineEvent? Evaluate(FeatureContext<WeatherInput> ctx)
    {
        var day = ctx.Current.Weather.NextHours(ctx.Now);
        var twoDays = ctx.Current.Weather.NextHours(ctx.Now, 48);
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
