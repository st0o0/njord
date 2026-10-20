using Njord.Core.Domain;
using Njord.Forecast;
using Njord.Forecast.AirQuality;
using Njord.Forecast.Weather;
using Njord.Messages.Pipeline;

namespace Njord.Weather.Lifestyle;

public sealed class OutdoorScoreHandler : IDomainHandler
{
    public const double GoodHourScore = 85.0;

    public string ConfigKey => "OutdoorScore";

    public DomainRequirements Requirements { get; } = new(
        Weather: WeatherParams.Temperature | WeatherParams.Precipitation | WeatherParams.PrecipitationProbability
            | WeatherParams.WindSpeed | WeatherParams.CloudCover,
        AirQuality: AirQualityParams.Pm25,
        OptionalWeather: WeatherParams.UvIndex,
        OptionalAirQuality: AirQualityParams.BirchPollen | AirQualityParams.GrassPollen
            | AirQualityParams.Alder | AirQualityParams.Mugwort);

    public IReadOnlyList<PipelineEvent> Handle(LocationForecast current, LocationForecast? previous, TimeProvider time)
    {
        if (current.Weather is not { } weather || current.AirQuality is not { } airQuality)
        {
            return [];
        }

        var hours = ForecastQueries.Upcoming(weather, time.GetUtcNow(), 24)
            .Select(point => (Point: point, Air: ForecastQueries.ValueAt(airQuality, point.ValidFrom)))
            .Where(pair => pair.Air is not null)
            .ToList();
        if (hours.Count == 0)
        {
            return [];
        }

        var (score, start, length) = Scoring.BestWindow([.. hours.Select(pair => ScoreHour(pair.Point, pair.Air!))], GoodHourScore);
        return [new PipelineEvent.OutdoorScoreComputed(current.Location, score, hours[start].Point.ValidFrom, length)];
    }

    private static double ScoreHour(WeatherPoint weather, AirQualityPoint air)
    {
        if (weather is not { Temperature: { } airTemperature, PrecipitationProbability: { } probability, WindSpeed: { } windSpeed, CloudCover: { } cloudCover }
            || air.Pm25 is not { } pm25)
        {
            return 0.0;
        }

        var temperature = Scoring.Band(airTemperature, 0.0, 15.0, 26.0, 36.0);
        var rain = weather.Precipitation > 0.1 ? 0.0 : 1.0 - Scoring.Ramp(probability, 0.0, 100.0);
        var wind = Scoring.Band(windSpeed, -1.0, 0.0, 8.0, 16.0);
        var cloud = 1.0 - 0.5 * Scoring.Ramp(cloudCover, 50.0, 100.0);
        var uv = weather.UvIndex is { } uvIndex ? 1.0 - Scoring.Ramp(uvIndex, 5.0, 11.0) : 1.0;
        var pollen = new[] { air.BirchPollen, air.GrassPollen, air.Alder, air.Mugwort }.Max() ?? 0.0;
        var airScore = Math.Min(1.0 - Scoring.Ramp(pm25, 15.0, 55.0), 1.0 - Scoring.Ramp(pollen, 20.0, 100.0));
        return 100.0 * (0.3 * temperature + 0.2 * rain + 0.1 * wind + 0.1 * cloud + 0.1 * uv + 0.2 * airScore);
    }
}
