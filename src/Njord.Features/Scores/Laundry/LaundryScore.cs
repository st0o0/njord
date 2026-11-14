using Microsoft.Extensions.Options;
using Njord.Core.Domain;
using Njord.Core.Forecast;
using Njord.Features.Shared;
using Njord.Messages.Results;
using Njord.Messages.Results.Scores;
using Njord.Messages.Stream;

namespace Njord.Features.Scores.Laundry;

public sealed class LaundryScore(IOptions<LaundryConfig> config) : FeatureRule<ForecastInput>
{
    public override FeatureDescriptor Descriptor { get; } = new(
        OutputKind.Score,
        FeatureCategory.Lifestyle,
        Name: "Laundry",
        new DomainRequirements(Forecast: ForecastParams.Precipitation | ForecastParams.Temperature | ForecastParams.Humidity
            | ForecastParams.WindSpeed | ForecastParams.PrecipitationProbability | ForecastParams.CloudCover,
        OptionalForecast: ForecastParams.SunshineDuration | ForecastParams.IsDay),
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

        var scores = hours.Select(h => ScoreHour(h, cfg)).ToList();
        var windows = WindowScanner.Scan(
            [.. hours.Select((hour, i) => new HourlyScore(hour.ValidFrom, scores[i]))],
            cfg.MinWindowScore,
            cfg.MinWindowHours,
            cfg.MaxWindows);
        var (start, length) = Scoring.LongestRun([.. scores.Select(score => score >= cfg.GoodHourScore)]);
        if (length == 0)
        {
            var bestIndex = scores.IndexOf(scores.Max());
            return new LaundryScoreResult(ctx.Location, (int)Math.Round(scores[bestIndex]), hours[bestIndex].ValidFrom, 0) { Windows = windows };
        }

        var windowScore = scores.Skip(start).Take(length).Average();
        return new LaundryScoreResult(ctx.Location, (int)Math.Round(windowScore), hours[start].ValidFrom, length) { Windows = windows };
    }

    internal static double ScoreHour(ForecastPoint point, LaundryConfig cfg)
    {
        if (point.IsDay is not > 0 || point.Precipitation > cfg.RainThreshold)
        {
            return 0.0;
        }

        if (point is not { Temperature: { } air, Humidity: { } relativeHumidity, WindSpeed: { } windSpeed, PrecipitationProbability: { } probability })
        {
            return 0.0;
        }

        var temperature = Scoring.Ramp(air, cfg.TemperatureLow, cfg.TemperatureHigh);
        var humidity = 1.0 - Scoring.Ramp(relativeHumidity, cfg.HumidityLow, cfg.HumidityHigh);
        var wind = Scoring.Band(windSpeed, cfg.WindLow, cfg.WindIdeal, cfg.WindModerate, cfg.WindStrong);
        var rain = 1.0 - Scoring.Ramp(probability, 0.0, 100.0);
        var sun = point.SunshineDuration is { } sunshine
            ? Scoring.Ramp(sunshine, 0.0, cfg.SunshineFull)
            : point.CloudCover is { } cloudCover ? 1.0 - Scoring.Ramp(cloudCover, 0.0, 100.0) : 0.0;
        return 100.0 * (cfg.TemperatureWeight * temperature + cfg.HumidityWeight * humidity
            + cfg.WindWeight * wind + cfg.RainWeight * rain + cfg.SunWeight * sun);
    }
}
