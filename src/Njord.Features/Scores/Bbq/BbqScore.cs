using Microsoft.Extensions.Options;
using Njord.Core.Domain;
using Njord.Core.Forecast;
using Njord.Features.Shared;
using Njord.Messages.Results;
using Njord.Messages.Results.Scores;
using Njord.Messages.Stream;

namespace Njord.Features.Scores.Bbq;

public sealed class BbqScore(IOptions<BbqConfig> config) : FeatureRule<ForecastInput>
{
    public override FeatureDescriptor Descriptor { get; } = new(
        OutputKind.Score,
        FeatureCategory.Lifestyle,
        Name: "Bbq",
        new DomainRequirements(Forecast: ForecastParams.Precipitation | ForecastParams.Temperature | ForecastParams.WindSpeed
            | ForecastParams.PrecipitationProbability | ForecastParams.CloudCover,
            OptionalForecast: ForecastParams.IsDay),
        EntityMetadata: new EntityMetadata(EntityPlatform.Sensor, Unit: "%"));

    public override bool Enabled => config.Value.Enabled;

    protected override ForecastInput? Project(LocationForecast forecast) =>
        forecast.Forecast is { } w ? new ForecastInput(w) : null;

    protected override PipelineEvent? Evaluate(FeatureContext<ForecastInput> ctx)
    {
        var cfg = config.Value;
        var hours = ctx.Current.Forecast.NextHours(ctx.Now);
        if (hours.Count == 0)
        {
            return null;
        }

        var (score, start, length) = Scoring.BestWindow([.. hours.Select(h => ScoreHour(h, cfg))], cfg.GoodHourScore);
        return new BbqScoreResult(ctx.Location, score, hours[start].ValidFrom, length);
    }

    internal static double ScoreHour(ForecastPoint point, BbqConfig cfg)
    {
        if (point.IsDay is not > 0 || point.Precipitation > cfg.RainThreshold)
        {
            return 0.0;
        }

        if (point is not { Temperature: { } air, WindSpeed: { } windSpeed, PrecipitationProbability: { } probability, CloudCover: { } cloudCover })
        {
            return 0.0;
        }

        var temperature = Scoring.Band(air, cfg.TemperatureLow, cfg.TemperatureIdeal, cfg.TemperatureWarm, cfg.TemperatureHot);
        var wind = 1.0 - Scoring.Ramp(windSpeed, cfg.WindModerate, cfg.WindStrong);
        var rain = 1.0 - Scoring.Ramp(probability, cfg.RainProbabilityLow, cfg.RainProbabilityHigh);
        var cloud = 1.0 - Scoring.Ramp(cloudCover, cfg.CloudLow, cfg.CloudHigh);
        return 100.0 * (cfg.TemperatureWeight * temperature + cfg.WindWeight * wind + cfg.RainWeight * rain + cfg.CloudWeight * cloud);
    }
}
