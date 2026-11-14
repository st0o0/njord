using Microsoft.Extensions.Options;
using Njord.Core.AirQuality;
using Njord.Core.Domain;
using Njord.Core.Forecast;
using Njord.Features.Shared;
using Njord.Messages.Results;
using Njord.Messages.Results.Scores;
using Njord.Messages.Stream;

namespace Njord.Features.Scores.Outdoor;

public sealed class OutdoorScore(IOptions<OutdoorConfig> config) : FeatureRule<ForecastAqInput>
{
    public override FeatureDescriptor Descriptor { get; } = new(
        OutputKind.Score,
        FeatureCategory.Lifestyle,
        Name: "Outdoor",
        new DomainRequirements(
        Forecast: ForecastParams.Temperature | ForecastParams.Precipitation | ForecastParams.PrecipitationProbability
            | ForecastParams.WindSpeed | ForecastParams.CloudCover,
        AirQuality: AirQualityParams.Pm25,
        OptionalForecast: ForecastParams.UvIndex | ForecastParams.IsDay,
        OptionalAirQuality: AirQualityParams.BirchPollen | AirQualityParams.GrassPollen
            | AirQualityParams.Alder | AirQualityParams.Mugwort),
        EntityMetadata: new EntityMetadata(EntityPlatform.Sensor, Unit: "%"));

    public override bool Enabled => config.Value.Enabled;

    protected override ForecastAqInput? Project(LocationForecast forecast) =>
        forecast is { Forecast: { } w, AirQuality: { } aq } ? new ForecastAqInput(w, aq) : null;

    protected override PipelineEvent? Evaluate(FeatureContext<ForecastAqInput> ctx)
    {
        var cfg = config.Value;
        var hours = ctx.Current.Forecast.NextHours(ctx.Now)
            .Select(point => (Point: point, Air: ForecastQueries.ValueAt(ctx.Current.AirQuality, point.ValidFrom)))
            .Where(pair => pair.Air is not null)
            .ToList();
        if (hours.Count == 0)
        {
            return null;
        }

        var (score, start, length) = Scoring.BestWindow([.. hours.Select(pair => ScoreHour(pair.Point, pair.Air!, cfg))], cfg.GoodHourScore);
        return new OutdoorScoreResult(ctx.Location, score, hours[start].Point.ValidFrom, length);
    }

    internal static double ScoreHour(ForecastPoint weather, AirQualityPoint air, OutdoorConfig cfg)
    {
        if (weather.IsDay is not > 0
            || weather is not { Temperature: { } airTemperature, PrecipitationProbability: { } probability, WindSpeed: { } windSpeed, CloudCover: { } cloudCover }
            || air.Pm25 is not { } pm25)
        {
            return 0.0;
        }

        var temperature = Scoring.Band(airTemperature, cfg.TemperatureLow, cfg.TemperatureIdeal, cfg.TemperatureWarm, cfg.TemperatureHot);
        var rain = weather.Precipitation > cfg.RainThreshold ? 0.0 : 1.0 - Scoring.Ramp(probability, 0.0, 100.0);
        var wind = Scoring.Band(windSpeed, cfg.WindLow, cfg.WindIdeal, cfg.WindModerate, cfg.WindStrong);
        var cloud = 1.0 - 0.5 * Scoring.Ramp(cloudCover, cfg.CloudLow, cfg.CloudHigh);
        var uv = weather.UvIndex is { } uvIndex ? 1.0 - Scoring.Ramp(uvIndex, cfg.UvModerate, cfg.UvHigh) : 1.0;
        var pollen = new[] { air.BirchPollen, air.GrassPollen, air.Alder, air.Mugwort }.Max() ?? 0.0;
        var airScore = Math.Min(1.0 - Scoring.Ramp(pm25, cfg.Pm25Low, cfg.Pm25High), 1.0 - Scoring.Ramp(pollen, cfg.PollenLow, cfg.PollenHigh));
        return 100.0 * (cfg.TemperatureWeight * temperature + cfg.RainWeight * rain + cfg.WindWeight * wind
            + cfg.CloudWeight * cloud + cfg.UvWeight * uv + cfg.AirQualityWeight * airScore);
    }
}
