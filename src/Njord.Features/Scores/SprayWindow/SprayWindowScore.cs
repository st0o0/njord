using Microsoft.Extensions.Options;
using Njord.Core.Domain;
using Njord.Core.Forecast;
using Njord.Features.Shared;
using Njord.Messages.Pipeline;
using Njord.Messages.Results;
using Njord.Messages.Stream;

namespace Njord.Features.Scores.SprayWindow;

public sealed class SprayWindowScore(IOptions<SprayWindowConfig> config) : FeatureRule<ForecastInput>
{
    public const double RainProbabilityLimit = 20.0;
    public const double TemperatureFalloff = 10.0;
    public const double HumidityMin = 40.0;
    public const double HumidityMax = 80.0;
    public const double HumidityFalloff = 20.0;

    public override FeatureDescriptor Descriptor { get; } = new(
        OutputKind.Score,
        FeatureCategory.Lifestyle,
        Name: "SprayWindow",
        DefaultEnabled: false,
        new DomainRequirements(Forecast: ForecastParams.WindSpeed | ForecastParams.Rain
            | ForecastParams.PrecipitationProbability | ForecastParams.Temperature | ForecastParams.Humidity));

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

        var options = config.Value;
        var scores = hours.Select((_, i) => ScoreHour(hours, i, options)).ToList();
        var windows = WindowScanner.Scan(scores, options.MinWindowScore, options.MinWindowHours, options.MaxWindows);
        if (windows.Count > 0)
        {
            var best = windows.OrderByDescending(window => window.Score).ThenBy(window => window.Start).First();
            return new SprayWindowScoreResult(ctx.Location, best.Score, best.Start, (int)(best.End - best.Start).TotalHours) { Windows = windows };
        }

        var top = scores.MaxBy(score => score.Score)!;
        return new SprayWindowScoreResult(ctx.Location, (int)Math.Round(top.Score), top.Time, 0) { Windows = windows };
    }

    private static HourlyScore ScoreHour(IReadOnlyList<ForecastPoint> hours, int index, SprayWindowConfig options)
    {
        var point = hours[index];
        if (point is not { WindSpeed: { } wind, Temperature: { } temperature, Humidity: { } humidity })
        {
            return new HourlyScore(point.ValidFrom, 0.0);
        }

        if (wind > options.MaxWindSpeed)
        {
            return new HourlyScore(point.ValidFrom, 0.0, LimitingKind.Wind, wind);
        }

        var upcoming = hours.Skip(index).Take(Math.Max(1, options.RainFreeHours));
        var wet = upcoming.FirstOrDefault(p => p.Rain is > 0.0 || p.PrecipitationProbability is >= RainProbabilityLimit);
        if (wet is not null)
        {
            return new HourlyScore(point.ValidFrom, 0.0, LimitingKind.Rain, wet.Rain ?? wet.PrecipitationProbability ?? 0.0);
        }

        var temperatureScore = 20.0 * Falloff(temperature, options.MinTemp, options.MaxTemp, TemperatureFalloff);
        var humidityScore = 10.0 * Falloff(humidity, HumidityMin, HumidityMax, HumidityFalloff);
        var total = 70.0 + temperatureScore + humidityScore;

        LimitingKind? worst = null;
        double? worstValue = null;
        if (temperatureScore < 20.0 && 20.0 - temperatureScore >= 10.0 - humidityScore)
        {
            worst = temperature < options.MinTemp ? LimitingKind.TemperatureLow : LimitingKind.TemperatureHigh;
            worstValue = temperature;
        }
        else if (humidityScore < 10.0)
        {
            worst = LimitingKind.Humidity;
            worstValue = humidity;
        }

        return new HourlyScore(point.ValidFrom, total, worst, worstValue);
    }

    private static double Falloff(double value, double min, double max, double width)
    {
        var distance = value < min ? min - value : value > max ? value - max : 0.0;
        return Math.Max(0.0, 1.0 - distance / width);
    }
}
