using Njord.Core.Domain;
using Njord.Forecast;
using Njord.Forecast.Weather;
using Njord.Messages.Pipeline;

namespace Njord.Weather.Lifestyle;

public sealed class BBQScoreHandler : IDomainHandler
{
    public const double GoodHourScore = 70.0;

    public string ConfigKey => "BBQScore";

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

        var (score, start, length) = Scoring.BestWindow([.. hours.Select(ScoreHour)], GoodHourScore);
        return [new PipelineEvent.BBQScoreComputed(current.Location, score, hours[start].ValidFrom, length)];
    }

    private static double ScoreHour(WeatherPoint point)
    {
        if (point.Precipitation > 0.1)
        {
            return 0.0;
        }

        var temperature = Scoring.Band(point.Temperature, 10.0, 18.0, 28.0, 36.0);
        var wind = 1.0 - Scoring.Ramp(point.WindSpeed, 5.0, 10.0);
        var rain = 1.0 - Scoring.Ramp(point.PrecipitationProbability, 0.0, 60.0);
        var cloud = 1.0 - Scoring.Ramp(point.CloudCover, 40.0, 100.0);
        return 100.0 * (0.35 * temperature + 0.2 * wind + 0.3 * rain + 0.15 * cloud);
    }
}
