using Njord.Core.Domain;
using Njord.Forecast.Weather;
using Njord.Messages.Pipeline;

namespace Njord.Weather.Lifestyle;

public sealed class GardeningHandler : IDomainHandler
{
    public const double FrostTemperature = 2.0;
    public const double RainThreshold = 2.0;
    public const double WindLimit = 10.0;

    public string Name => "Gardening";

    public FeatureDescriptor Feature { get; } = new(
        OutputKind.Condition,
        FeatureCategory.Lifestyle,
        DefaultEnabled: false,
        new DomainRequirements(Weather: WeatherParams.Temperature | WeatherParams.WindSpeed | WeatherParams.Precipitation));

    public IReadOnlyList<PipelineEvent> Handle(LocationForecast current, LocationForecast? previous, TimeProvider time)
    {
        if (current.Weather is not { } weather)
        {
            return [];
        }

        var now = time.GetUtcNow();
        var day = ForecastWindow.NextHours(weather, now);
        var twoDays = ForecastWindow.NextHours(weather, now, 48);
        if (day.Count == 0)
        {
            return [];
        }

        var frostRisk = day.Min(p => p.Temperature) < FrostTemperature;
        var frostWithinTwoDays = twoDays.Min(p => p.Temperature) < FrostTemperature;
        var windy = day.Max(p => p.WindSpeed) >= WindLimit;
        var wateringNeeded = day.Sum(p => p.Precipitation) < RainThreshold;
        return [new ConditionResult(current.Location, this.WireKey(), !frostWithinTwoDays && !windy ? "plant" : "hold", FeatureData.Of(
            ("planting_safe", FeatureData.Flag(!frostWithinTwoDays && !windy)),
            ("watering_needed", FeatureData.Flag(wateringNeeded)),
            ("frost_risk", FeatureData.Flag(frostRisk))))];
    }
}
