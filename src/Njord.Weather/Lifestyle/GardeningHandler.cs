using Njord.Core.Domain;
using Njord.Forecast;
using Njord.Forecast.Weather;
using Njord.Messages.Pipeline;

namespace Njord.Weather.Lifestyle;

public sealed class GardeningHandler : IDomainHandler
{
    public const double FrostTemperature = 2.0;
    public const double RainThreshold = 2.0;
    public const double WindLimit = 10.0;

    public string ConfigKey => "GardeningAdvisory";

    public DomainRequirements Requirements { get; } = new(Weather: WeatherParams.Temperature | WeatherParams.WindSpeed | WeatherParams.Precipitation);

    public IReadOnlyList<PipelineEvent> Handle(LocationForecast current, LocationForecast? previous, TimeProvider time)
    {
        if (current.Weather is not { } weather)
        {
            return [];
        }

        var now = time.GetUtcNow();
        var day = ForecastQueries.Upcoming(weather, now, 24);
        var twoDays = ForecastQueries.Upcoming(weather, now, 48);
        if (day.Count == 0)
        {
            return [];
        }

        var frostRisk = day.Min(p => p.Temperature) < FrostTemperature;
        var frostWithinTwoDays = twoDays.Min(p => p.Temperature) < FrostTemperature;
        var windy = day.Max(p => p.WindSpeed) >= WindLimit;
        var wateringNeeded = day.Sum(p => p.Precipitation) < RainThreshold;
        return [new PipelineEvent.GardeningAdvised(current.Location, !frostWithinTwoDays && !windy, wateringNeeded, frostRisk)];
    }
}
