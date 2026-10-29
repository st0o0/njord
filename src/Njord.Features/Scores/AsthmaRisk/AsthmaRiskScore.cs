using Microsoft.Extensions.Options;
using Njord.Core.AirQuality;
using Njord.Core.Domain;
using Njord.Core.Forecast;
using Njord.Features.Shared;
using Njord.Messages.Results;
using Njord.Messages.Results.Scores;
using Njord.Messages.Stream;

namespace Njord.Features.Scores.AsthmaRisk;

public sealed class AsthmaRiskScore(IOptions<AsthmaRiskConfig> config) : FeatureRule<ForecastAqInput>
{
    public const double Pm25Reference = 75.0;
    public const double OzoneReference = 180.0;
    public const double PollenReference = 100.0;
    public const double GoodHourScore = 60.0;
    public const double ColdAirFactor = 1.3;
    public const double RapidChangeFactor = 1.2;
    public const double RapidChangeDegrees = 6.0;
    public const int RapidChangeHours = 3;

    public override FeatureDescriptor Descriptor { get; } = new(
        OutputKind.Score,
        FeatureCategory.AirQuality,
        Name: "AsthmaRisk",
        new DomainRequirements(
            Forecast: ForecastParams.Temperature | ForecastParams.Humidity | ForecastParams.BoundaryLayerHeight,
            AirQuality: AirQualityParams.Pm25 | AirQualityParams.Ozone | AirQualityParams.BirchPollen | AirQualityParams.GrassPollen));

    public override bool Enabled => config.Value.Enabled;

    protected override ForecastAqInput? Project(LocationForecast forecast) =>
        forecast is { Forecast: { } w, AirQuality: { } aq } ? new(w, aq) : null;

    protected override PipelineEvent? Evaluate(FeatureContext<ForecastAqInput> ctx)
    {
        var cfg = config.Value;
        var weatherByTime = ctx.Current.Forecast.Points.ToDictionary(p => p.ValidFrom);
        var risks = new List<(DateTimeOffset Time, double Risk)>();
        foreach (var air in ctx.Current.AirQuality.NextHours(ctx.Now))
        {
            if (!weatherByTime.TryGetValue(air.ValidFrom, out var weather))
            {
                continue;
            }

            weatherByTime.TryGetValue(air.ValidFrom.AddHours(-RapidChangeHours), out var earlier);
            if (Risk(cfg, air, weather, earlier) is { } risk)
            {
                risks.Add((air.ValidFrom, risk));
            }
        }

        if (risks.Count == 0)
        {
            return null;
        }

        var goodness = risks.Select(r => 100.0 - r.Risk).ToList();
        var windows = WindowScanner.Scan(
            [.. risks.Select((r, i) => new HourlyScore(r.Time, goodness[i]))],
            cfg.MinWindowScore,
            cfg.MinWindowHours,
            cfg.MaxWindows);
        var peak = (int)Math.Round(risks.Max(r => r.Risk));
        var (start, length) = Scoring.LongestRun([.. goodness.Select(g => g >= GoodHourScore)]);
        if (length == 0)
        {
            var bestIndex = goodness.IndexOf(goodness.Max());
            return new AsthmaRiskScoreResult(ctx.Location, peak, risks[bestIndex].Time, 0) { Windows = windows };
        }

        return new AsthmaRiskScoreResult(ctx.Location, peak, risks[start].Time, length) { Windows = windows };
    }

    private static double? Risk(AsthmaRiskConfig cfg, AirQualityPoint air, ForecastPoint weather, ForecastPoint? earlier)
    {
        var parts = new List<(double Weight, double Value)>();
        if (air.Pm25 is { } pm25)
        {
            parts.Add((cfg.Pm25Weight, Scoring.Ramp(pm25, 0.0, Pm25Reference) * 100.0));
        }

        if (air.Ozone is { } ozone)
        {
            parts.Add((cfg.OzoneWeight, Scoring.Ramp(ozone, 0.0, OzoneReference) * 100.0));
        }

        var pollen = Math.Max(air.BirchPollen ?? 0.0, air.GrassPollen ?? 0.0);
        parts.Add((cfg.PollenWeight, Scoring.Ramp(pollen, 0.0, PollenReference) * 100.0));

        if (weather.Humidity is { } humidity)
        {
            var dryness = Scoring.Ramp(humidity, 40.0, 20.0);
            var dampness = Scoring.Ramp(humidity, 60.0, 80.0);
            parts.Add((cfg.HumidityWeight, Math.Max(dryness, dampness) * 100.0));
        }

        if (weather.BoundaryLayerHeight is { } boundary)
        {
            parts.Add((cfg.BoundaryLayerWeight, Scoring.Ramp(boundary, 2000.0, 200.0) * 100.0));
        }

        var totalWeight = parts.Sum(p => p.Weight);
        if (totalWeight <= 0.0 || parts.Count < 2)
        {
            return null;
        }

        var risk = parts.Sum(p => p.Weight * p.Value) / totalWeight;
        if (weather is { Temperature: < 5.0, Humidity: > 80.0 })
        {
            risk *= ColdAirFactor;
        }

        if (weather.Temperature is { } now && earlier?.Temperature is { } before
            && Math.Abs(now - before) >= RapidChangeDegrees)
        {
            risk *= RapidChangeFactor;
        }

        return Math.Clamp(risk, 0.0, 100.0);
    }
}
