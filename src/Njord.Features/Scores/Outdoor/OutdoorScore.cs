using Microsoft.Extensions.Options;
using Njord.Core.Domain;
using Njord.Features.Shared;
using Njord.Forecast.AirQuality;
using Njord.Forecast.Weather;
using Njord.Messages.Pipeline;

namespace Njord.Features.Scores.Outdoor;

public sealed class OutdoorScore(IOptions<OutdoorConfig> config) : FeatureRule<WeatherAqInput>
{
    public const double GoodHourScore = 85.0;

    public override FeatureDescriptor Descriptor { get; } = new(
        OutputKind.Score,
        FeatureCategory.Lifestyle,
        DefaultEnabled: false,
        new DomainRequirements(
        Weather: WeatherParams.Temperature | WeatherParams.Precipitation | WeatherParams.PrecipitationProbability
            | WeatherParams.WindSpeed | WeatherParams.CloudCover,
        AirQuality: AirQualityParams.Pm25,
        OptionalWeather: WeatherParams.UvIndex,
        OptionalAirQuality: AirQualityParams.BirchPollen | AirQualityParams.GrassPollen
            | AirQualityParams.Alder | AirQualityParams.Mugwort));

    public override bool Enabled => config.Value.Enabled;

    protected override WeatherAqInput? Project(LocationForecast forecast) =>
        forecast is { Weather: { } w, AirQuality: { } aq } ? new(w, aq) : null;

    protected override PipelineEvent? Evaluate(FeatureContext<WeatherAqInput> ctx)
    {
        var hours = ctx.Current.Weather.NextHours(ctx.Now)
            .Select(point => (Point: point, Air: ForecastQueries.ValueAt(ctx.Current.AirQuality, point.ValidFrom)))
            .Where(pair => pair.Air is not null)
            .ToList();
        if (hours.Count == 0)
        {
            return null;
        }

        var (score, start, length) = Scoring.BestWindow([.. hours.Select(pair => ScoreHour(pair.Point, pair.Air!))], GoodHourScore);
        return new OutdoorScoreResult(ctx.Location, score, hours[start].Point.ValidFrom, length);
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
