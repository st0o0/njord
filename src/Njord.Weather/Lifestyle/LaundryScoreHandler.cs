using Njord.Core.Domain;
using Njord.Messages.Pipeline;
using Njord.Model;

namespace Njord.Weather.Lifestyle;

public sealed class LaundryScoreHandler : IDomainHandler
{
    public const double GoodHourScore = 60.0;

    public string ConfigKey => "LaundryScore";

    public IReadOnlySet<DomainKind> Triggers { get; } = new HashSet<DomainKind> { DomainKind.Weather };

    public IReadOnlyList<PipelineEvent> Handle(LocationForecast current, LocationForecast? previous, TimeProvider time)
    {
        if (current.Weather is not { } weather)
        {
            return [];
        }

        var hours = ForecastQueries.Upcoming(weather, time.GetUtcNow(), 24);
        if (hours.Count == 0)
        {
            return [];
        }

        var scores = hours.Select(ScoreHour).ToList();
        var (start, length) = Scoring.LongestRun([.. scores.Select(score => score >= GoodHourScore)]);
        if (length == 0)
        {
            var bestIndex = scores.IndexOf(scores.Max());
            return [new PipelineEvent.LaundryScoreComputed(current.Location, (int)Math.Round(scores[bestIndex]), hours[bestIndex].ValidFrom, 0)];
        }

        var windowScore = scores.Skip(start).Take(length).Average();
        return [new PipelineEvent.LaundryScoreComputed(current.Location, (int)Math.Round(windowScore), hours[start].ValidFrom, length)];
    }

    private static double ScoreHour(WeatherPoint point)
    {
        if (point.Precipitation > 0.1)
        {
            return 0.0;
        }

        var temperature = Scoring.Ramp(point.Temperature, 5.0, 25.0);
        var humidity = 1.0 - Scoring.Ramp(point.Humidity, 40.0, 90.0);
        var wind = Scoring.Band(point.WindSpeed, 0.0, 2.0, 8.0, 15.0);
        var rain = 1.0 - Scoring.Ramp(point.PrecipitationProbability, 0.0, 100.0);
        var sun = point.SunshineDuration is { } sunshine
            ? Scoring.Ramp(sunshine, 0.0, 3600.0)
            : 1.0 - Scoring.Ramp(point.CloudCover, 0.0, 100.0);
        return 100.0 * (0.25 * temperature + 0.25 * humidity + 0.15 * wind + 0.2 * rain + 0.15 * sun);
    }
}
