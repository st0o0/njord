using Microsoft.Extensions.Options;
using Njord.Core.AirQuality;
using Njord.Core.Domain;
using Njord.Core.Forecast;
using Njord.Features.Shared;
using Njord.Messages.Pipeline;
using Njord.Messages.Results;
using Njord.Messages.Stream;

namespace Njord.Features.Scores.AllergySeverity;

public sealed class AllergySeverityScore(IOptions<AllergySeverityConfig> config) : FeatureRule<ForecastAqInput>
{
    public const double PollenReference = 100.0;
    public const double GoodHourScore = 60.0;

    public override FeatureDescriptor Descriptor { get; } = new(
        OutputKind.Score,
        FeatureCategory.AirQuality,
        Name: "AllergySeverity",
        DefaultEnabled: false,
        new DomainRequirements(
            Forecast: ForecastParams.WindSpeed | ForecastParams.Humidity | ForecastParams.Rain | ForecastParams.BoundaryLayerHeight,
            AirQuality: AirQualityParams.BirchPollen | AirQualityParams.GrassPollen | AirQualityParams.Alder | AirQualityParams.Mugwort));

    public override bool Enabled => config.Value.Enabled;

    protected override ForecastAqInput? Project(LocationForecast forecast) =>
        forecast is { Forecast: { } w, AirQuality: { } aq } ? new(w, aq) : null;

    protected override PipelineEvent? Evaluate(FeatureContext<ForecastAqInput> ctx)
    {
        var weatherByTime = ctx.Current.Forecast.NextHours(ctx.Now).ToDictionary(p => p.ValidFrom);
        var severities = new List<(DateTimeOffset Time, double Severity)>();
        foreach (var air in ctx.Current.AirQuality.NextHours(ctx.Now))
        {
            if (!weatherByTime.TryGetValue(air.ValidFrom, out var weather))
            {
                continue;
            }

            weatherByTime.TryGetValue(air.ValidFrom.AddHours(-1), out var previous);
            if (Severity(air, weather, previous) is { } severity)
            {
                severities.Add((air.ValidFrom, severity));
            }
        }

        if (severities.Count == 0)
        {
            return null;
        }

        var goodness = severities.Select(s => 100.0 - s.Severity).ToList();
        var windows = WindowScanner.Scan(
            [.. severities.Select((s, i) => new HourlyScore(s.Time, goodness[i]))],
            config.Value.MinWindowScore,
            config.Value.MinWindowHours,
            config.Value.MaxWindows);
        var peak = (int)Math.Round(severities.Max(s => s.Severity));
        var (start, length) = Scoring.LongestRun([.. goodness.Select(g => g >= GoodHourScore)]);
        if (length == 0)
        {
            var bestIndex = goodness.IndexOf(goodness.Max());
            return new AllergySeverityScoreResult(ctx.Location, peak, severities[bestIndex].Time, 0) { Windows = windows };
        }

        return new AllergySeverityScoreResult(ctx.Location, peak, severities[start].Time, length) { Windows = windows };
    }

    private static double? Severity(AirQualityPoint air, ForecastPoint weather, ForecastPoint? previous)
    {
        double?[] species = [air.BirchPollen, air.GrassPollen, air.Alder, air.Mugwort];
        var present = species.OfType<double>().ToList();
        if (present.Count == 0)
        {
            return null;
        }

        var score = Math.Min(100.0, present.Max() / PollenReference * 100.0);
        if (score <= 0.0)
        {
            return 0.0;
        }

        if (weather.WindSpeed > 4.0 && weather.Humidity < 60.0)
        {
            score += 15.0;
        }

        if (weather.BoundaryLayerHeight < 800.0)
        {
            score += 15.0;
        }

        if (weather.Humidity < 40.0)
        {
            score += 10.0;
        }

        if (weather.Rain > 0.1 || previous?.Rain > 0.1)
        {
            score -= 30.0;
        }

        if (weather.Humidity > 80.0)
        {
            score -= 15.0;
        }

        return Math.Clamp(score, 0.0, 100.0);
    }
}
