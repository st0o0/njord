using Njord.Core.Domain;
using Njord.Forecast.Weather;
using Njord.Messages.Pipeline;

namespace Njord.Scores;

public sealed class LaundryScoreHandler : IDomainHandler
{
    public const double GoodHourScore = 60.0;

    public string Name => "Laundry";

    public FeatureDescriptor Feature { get; } = new(
        OutputKind.Score,
        FeatureCategory.Lifestyle,
        DefaultEnabled: false,
        new DomainRequirements(Weather: WeatherParams.Precipitation | WeatherParams.Temperature | WeatherParams.Humidity
            | WeatherParams.WindSpeed | WeatherParams.PrecipitationProbability | WeatherParams.CloudCover,
        OptionalWeather: WeatherParams.SunshineDuration));

    public IReadOnlyList<PipelineEvent> Handle(LocationForecast current, LocationForecast? previous, TimeProvider time)
    {
        if (current.Weather is not { } weather)
        {
            return [];
        }

        var hours = ForecastWindow.NextHours(weather, time.GetUtcNow());
        if (hours.Count == 0)
        {
            return [];
        }

        var scores = hours.Select(ScoreHour).ToList();
        var (start, length) = Scoring.LongestRun([.. scores.Select(score => score >= GoodHourScore)]);
        if (length == 0)
        {
            var bestIndex = scores.IndexOf(scores.Max());
            return [new ScoreResult(current.Location, this.WireKey(), (int)Math.Round(scores[bestIndex]), hours[bestIndex].ValidFrom, 0)];
        }

        var windowScore = scores.Skip(start).Take(length).Average();
        return [new ScoreResult(current.Location, this.WireKey(), (int)Math.Round(windowScore), hours[start].ValidFrom, length)];
    }

    private static double ScoreHour(WeatherPoint point)
    {
        if (point.Precipitation > 0.1)
        {
            return 0.0;
        }

        if (point is not { Temperature: { } air, Humidity: { } relativeHumidity, WindSpeed: { } windSpeed, PrecipitationProbability: { } probability })
        {
            return 0.0;
        }

        var temperature = Scoring.Ramp(air, 5.0, 25.0);
        var humidity = 1.0 - Scoring.Ramp(relativeHumidity, 40.0, 90.0);
        var wind = Scoring.Band(windSpeed, 0.0, 2.0, 8.0, 15.0);
        var rain = 1.0 - Scoring.Ramp(probability, 0.0, 100.0);
        var sun = point.SunshineDuration is { } sunshine
            ? Scoring.Ramp(sunshine, 0.0, 3600.0)
            : point.CloudCover is { } cloudCover ? 1.0 - Scoring.Ramp(cloudCover, 0.0, 100.0) : 0.0;
        return 100.0 * (0.25 * temperature + 0.25 * humidity + 0.15 * wind + 0.2 * rain + 0.15 * sun);
    }
}
