using Microsoft.Extensions.Options;
using Njord.Core.Domain;
using Njord.Features.Alerts.Shared;
using Njord.Forecast.Weather;
using Njord.Messages.Alerts;
using Njord.Messages.Pipeline;

namespace Njord.Features.Alerts.Snow;

public sealed class SnowAlert(IOptions<SnowConfig> config) : FeatureRule<WeatherEnsembleInput>
{
    public override FeatureDescriptor Descriptor { get; } = new(
        OutputKind.Alert,
        FeatureCategory.Weather,
        Name: "Snow",
        DefaultEnabled: true,
        new DomainRequirements(Weather: WeatherParams.Snowfall | WeatherParams.FreezingLevelHeight, Ensemble: EnsembleParams.TemperatureSpread));

    public override bool Enabled => config.Value.Enabled;

    protected override WeatherEnsembleInput? Project(LocationForecast forecast) =>
        forecast is { Weather: { } w, Ensemble: { } e } ? new(w, e) : null;

    protected override PipelineEvent? Evaluate(FeatureContext<WeatherEnsembleInput> ctx)
    {
        var window = ctx.Current.Weather.NextHoursInclusive(ctx.Now);
        if (!window.Any(p => p.Snowfall is not null))
        {
            return null;
        }

        var totalSum = window.Sum(p => p.Snowfall ?? 0.0);
        var snowiest = window.HighestBy(p => p.Snowfall);
        var freezingLevels = window.Select(p => p.FreezingLevelHeight).OfType<double>().Order().ToList();

        var (severity, snowThreshold) = totalSum switch
        {
            > 20 => (AlertSeverity.Red, 20.0),
            > 5 => (AlertSeverity.Orange, 5.0),
            > 0 => (AlertSeverity.Yellow, 0.0),
            _ => (AlertSeverity.None, 0.0),
        };

        if (severity == AlertSeverity.None)
        {
            return null;
        }

        var roundedSum = Math.Round(totalSum, 1);
        var freezingLevel = freezingLevels.Count > 0 ? (double?)Math.Round(Median(freezingLevels), 0) : null;
        var spread = snowiest is not null ? ctx.Current.Ensemble.Points.SpreadAt(snowiest.ValidFrom) : null;
        var level = ConfidenceThresholds.Default.LevelFor(spread?.TemperatureSpread);
        return new SnowAlertResult(
            ctx.Location, severity, level.ToConfidence(), roundedSum, snowThreshold,
            PeakValue: null, HoursUntil: null, DurationHours: null, Level: level,
            ExpectedAccumulation: roundedSum, FreezingLevel: freezingLevel);
    }

    private static double Median(List<double> sorted)
    {
        var mid = sorted.Count / 2;
        return sorted.Count % 2 == 1 ? sorted[mid] : (sorted[mid - 1] + sorted[mid]) / 2.0;
    }
}
