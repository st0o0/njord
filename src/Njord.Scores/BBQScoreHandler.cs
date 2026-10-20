using Njord.Core.Domain;
using Njord.Forecast.Weather;
using Njord.Messages.Pipeline;

namespace Njord.Scores;

public sealed class BBQScoreHandler : IDomainHandler
{
    public const double GoodHourScore = 70.0;

    public string Name => "Bbq";

    public FeatureDescriptor Feature { get; } = new(
        OutputKind.Score,
        FeatureCategory.Lifestyle,
        DefaultEnabled: false,
        new DomainRequirements(Weather: WeatherParams.Precipitation | WeatherParams.Temperature | WeatherParams.WindSpeed
            | WeatherParams.PrecipitationProbability | WeatherParams.CloudCover));

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

        var (score, start, length) = Scoring.BestWindow([.. hours.Select(ScoreHour)], GoodHourScore);
        return [new ScoreResult(current.Location, this.WireKey(), score, hours[start].ValidFrom, length)];
    }

    private static double ScoreHour(WeatherPoint point)
    {
        if (point.Precipitation > 0.1)
        {
            return 0.0;
        }

        if (point is not { Temperature: { } air, WindSpeed: { } windSpeed, PrecipitationProbability: { } probability, CloudCover: { } cloudCover })
        {
            return 0.0;
        }

        var temperature = Scoring.Band(air, 10.0, 18.0, 28.0, 36.0);
        var wind = 1.0 - Scoring.Ramp(windSpeed, 5.0, 10.0);
        var rain = 1.0 - Scoring.Ramp(probability, 0.0, 60.0);
        var cloud = 1.0 - Scoring.Ramp(cloudCover, 40.0, 100.0);
        return 100.0 * (0.35 * temperature + 0.2 * wind + 0.3 * rain + 0.15 * cloud);
    }
}
