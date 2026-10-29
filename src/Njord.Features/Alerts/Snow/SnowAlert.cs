using Microsoft.Extensions.Options;
using Njord.Core.Domain;
using Njord.Core.Forecast;
using Njord.Features.Alerts.Shared;
using Njord.Messages.Alerts;
using Njord.Messages.Results;
using Njord.Messages.Results.Alerts;
using Njord.Messages.Stream;

namespace Njord.Features.Alerts.Snow;

public sealed class SnowAlert(IOptions<SnowConfig> config) : FeatureRule<ForecastEnsembleInput>
{
    public override FeatureDescriptor Descriptor { get; } = new(
        OutputKind.Alert,
        FeatureCategory.Forecast,
        Name: "Snow",
        new DomainRequirements(Forecast: ForecastParams.Snowfall | ForecastParams.FreezingLevelHeight, Ensemble: EnsembleParams.PrecipitationSpread));

    public override bool Enabled => config.Value.Enabled;

    protected override ForecastEnsembleInput? Project(LocationForecast forecast) =>
        forecast is { Forecast: { } w, Ensemble: { } e } ? new(w, e) : null;

    protected override PipelineEvent? Evaluate(FeatureContext<ForecastEnsembleInput> ctx)
    {
        var window = ctx.Current.Forecast.NextHoursInclusive(ctx.Now);
        if (!window.Any(p => p.Snowfall is not null))
        {
            return null;
        }

        var totalSum = window.Sum(p => p.Snowfall ?? 0.0);
        var snowiest = window.HighestBy(p => p.Snowfall);
        var freezingLevels = window.Select(p => p.FreezingLevelHeight).OfType<double>().Order().ToList();

        var (severity, snowThreshold) = config.Value.Thresholds.ToSeverity(ScanDirection.Max).Classify(totalSum);

        if (severity == AlertSeverity.None)
        {
            return null;
        }

        var roundedSum = Math.Round(totalSum, 1);
        var freezingLevel = freezingLevels.Count > 0 ? (double?)Math.Round(Median(freezingLevels), 0) : null;
        var spread = snowiest is not null ? ctx.Current.Ensemble.Points.SpreadAt(snowiest.ValidFrom) : null;
        var level = ConfidenceThresholds.Default.LevelFor(spread?.PrecipitationSpread);
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
