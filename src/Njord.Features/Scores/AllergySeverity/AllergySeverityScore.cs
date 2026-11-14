using Microsoft.Extensions.Options;
using Njord.Core.AirQuality;
using Njord.Core.Domain;
using Njord.Core.Forecast;
using Njord.Features.Shared;
using Njord.Messages.Results;
using Njord.Messages.Results.Scores;
using Njord.Messages.Stream;

namespace Njord.Features.Scores.AllergySeverity;

public sealed class AllergySeverityScore(IOptions<AllergySeverityConfig> config) : FeatureRule<ForecastAqInput>
{
    public override FeatureDescriptor Descriptor { get; } = new(
        OutputKind.Score,
        FeatureCategory.AirQuality,
        Name: "AllergySeverity",
        new DomainRequirements(
            Forecast: ForecastParams.WindSpeed | ForecastParams.Humidity | ForecastParams.Rain | ForecastParams.BoundaryLayerHeight,
            AirQuality: AirQualityParams.BirchPollen | AirQualityParams.GrassPollen | AirQualityParams.Alder | AirQualityParams.Mugwort),
        EntityMetadata: new EntityMetadata(EntityPlatform.Sensor, Unit: "%"));

    public override bool Enabled => config.Value.Enabled;

    protected override ForecastAqInput? Project(LocationForecast forecast) =>
        forecast is { Forecast: { } w, AirQuality: { } aq } ? new ForecastAqInput(w, aq) : null;

    protected override PipelineEvent? Evaluate(FeatureContext<ForecastAqInput> ctx)
    {
        var cfg = config.Value;
        var weatherByTime = ctx.Current.Forecast.NextHours(ctx.Now).ToDictionary(p => p.ValidFrom);
        var severities = new List<(DateTimeOffset Time, double Severity)>();
        foreach (var air in ctx.Current.AirQuality.NextHours(ctx.Now))
        {
            if (!weatherByTime.TryGetValue(air.ValidFrom, out var weather))
            {
                continue;
            }

            weatherByTime.TryGetValue(air.ValidFrom.AddHours(-1), out var previous);
            if (Severity(cfg, air, weather, previous) is { } severity)
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
            cfg.MinWindowScore,
            cfg.MinWindowHours,
            cfg.MaxWindows);
        var peak = (int)Math.Round(severities.Max(s => s.Severity));
        var (start, length) = Scoring.LongestRun([.. goodness.Select(g => g >= cfg.GoodHourScore)]);
        if (length == 0)
        {
            var bestIndex = goodness.IndexOf(goodness.Max());
            return new AllergySeverityScoreResult(ctx.Location, peak, severities[bestIndex].Time, 0) { Windows = windows };
        }

        return new AllergySeverityScoreResult(ctx.Location, peak, severities[start].Time, length) { Windows = windows };
    }

    private static double? Severity(AllergySeverityConfig cfg, AirQualityPoint air, ForecastPoint weather, ForecastPoint? previous)
    {
        double?[] species = [air.BirchPollen, air.GrassPollen, air.Alder, air.Mugwort];
        var present = species.OfType<double>().ToList();
        if (present.Count == 0)
        {
            return null;
        }

        var score = Math.Min(100.0, present.Max() / cfg.PollenReference * 100.0);
        if (score <= 0.0)
        {
            return 0.0;
        }

        if (weather is { WindSpeed: { } wind, Humidity: { } humidity }
            && wind > cfg.WindThreshold && humidity < cfg.DryHumidityThreshold)
        {
            score += cfg.WindPollenModifier;
        }

        if (weather.BoundaryLayerHeight < cfg.BoundaryLayerThreshold)
        {
            score += cfg.LowBoundaryLayerModifier;
        }

        if (weather.Humidity < cfg.DryAirHumidityThreshold)
        {
            score += cfg.DryAirModifier;
        }

        if (weather.Rain > 0.1 || previous?.Rain > 0.1)
        {
            score += cfg.RainModifier;
        }

        if (weather.Humidity > cfg.HighHumidityThreshold)
        {
            score += cfg.HighHumidityModifier;
        }

        return Math.Clamp(score, 0.0, 100.0);
    }
}
