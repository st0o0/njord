using Microsoft.Extensions.Options;
using Njord.Core.Domain;
using Njord.Core.Forecast;
using Njord.Features.Alerts.Shared;
using Njord.Messages.Alerts;
using Njord.Messages.Results;
using Njord.Messages.Results.Alerts;
using Njord.Messages.Stream;

namespace Njord.Features.Alerts.Storm;

public sealed class StormAlert(IOptions<StormConfig> config) : FeatureRule<ForecastEnsembleInput>
{
    public override FeatureDescriptor Descriptor { get; } = new(
        OutputKind.Alert,
        FeatureCategory.Forecast,
        Name: "Storm",
        new DomainRequirements(Forecast: ForecastParams.WindGusts, Ensemble: EnsembleParams.WindSpeedSpread));

    public override bool Enabled => config.Value.Enabled;

    protected override ForecastEnsembleInput? Project(LocationForecast forecast) =>
        forecast is { Forecast: { } w, Ensemble: { } e } ? new(w, e) : null;

    protected override PipelineEvent? Evaluate(FeatureContext<ForecastEnsembleInput> ctx)
    {
        var thresholds = config.Value.GustThresholds.ToSeverity(ScanDirection.Max);
        if (ctx.Current.Forecast.NextHoursInclusive(ctx.Now, config.Value.WindowHours).HighestBy(p => p.WindGusts) is not { WindGusts: { } gust } peak)
        {
            return null;
        }

        var (severity, effectiveThreshold) = thresholds.Classify(gust);
        if (severity == AlertSeverity.None)
        {
            return null;
        }

        var rounded = Math.Round(gust, 1);
        var spread = ctx.Current.Ensemble.Points.SpreadAt(peak.ValidFrom);
        var level = ConfidenceThresholds.Default.LevelFor(spread?.WindSpeedSpread);
        return new StormAlertResult(
            ctx.Location, severity, level.ToConfidence(), rounded, effectiveThreshold,
            PeakValue: null, HoursUntil: peak.HoursFrom(ctx.Now), DurationHours: null, Level: level,
            ExpectedMaxGust: rounded);
    }
}
