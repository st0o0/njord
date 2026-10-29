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
    public const double GoodHourScore = 60.0;

    public override FeatureDescriptor Descriptor { get; } = new(
        OutputKind.Score,
        FeatureCategory.Lifestyle,
        Name: "Laundry",
        new DomainRequirements(Forecast: ForecastParams.Precipitation | ForecastParams.Temperature | ForecastParams.Humidity
            | ForecastParams.WindSpeed | ForecastParams.PrecipitationProbability | ForecastParams.CloudCover,
        OptionalForecast: ForecastParams.SunshineDuration | ForecastParams.IsDay));

    public override bool Enabled => config.Value.Enabled;

    protected override ForecastInput? Project(LocationForecast forecast) =>
        forecast.Forecast is { } w ? new(w) : null;

    protected override PipelineEvent? Evaluate(FeatureContext<ForecastInput> ctx)
    {
        var hours = ctx.Current.Forecast.NextHours(ctx.Now);
        if (hours.Count == 0)
        {
            return null;
        }

        var scores = hours.Select(ScoreHour).ToList();
        var windows = WindowScanner.Scan(
            [.. hours.Select((hour, i) => new HourlyScore(hour.ValidFrom, scores[i]))],
            config.Value.MinWindowScore,
            config.Value.MinWindowHours,
            config.Value.MaxWindows);
        var (start, length) = Scoring.LongestRun([.. scores.Select(score => score >= GoodHourScore)]);
        if (length == 0)
        {
            var bestIndex = scores.IndexOf(scores.Max());
            return new LaundryScoreResult(ctx.Location, (int)Math.Round(scores[bestIndex]), hours[bestIndex].ValidFrom, 0) { Windows = windows };
        }

        var windowScore = scores.Skip(start).Take(length).Average();
        return new LaundryScoreResult(ctx.Location, (int)Math.Round(windowScore), hours[start].ValidFrom, length) { Windows = windows };
    }

    private static double ScoreHour(ForecastPoint point)
    {
        if (point.IsDay is not > 0 || point.Precipitation > 0.1)
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
