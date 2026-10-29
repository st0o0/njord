using Microsoft.Extensions.Options;
using Njord.Core.Domain;
using Njord.Core.Forecast;
using Njord.Features.Alerts.Shared;
using Njord.Messages.Alerts;
using Njord.Messages.Results;
using Njord.Messages.Results.Alerts;
using Njord.Messages.Stream;

namespace Njord.Features.Alerts.WetBulbDanger;

public sealed class WetBulbDangerAlert(IOptions<WetBulbDangerConfig> config) : FeatureRule<ForecastEnsembleInput>
{
    public override FeatureDescriptor Descriptor { get; } = new(
        OutputKind.Alert,
        FeatureCategory.Forecast,
        Name: "WetBulbDanger",
        DefaultEnabled: false,
        new DomainRequirements(Forecast: ForecastParams.WetBulbTemperature, Ensemble: EnsembleParams.TemperatureSpread));

    public override bool Enabled => config.Value.Enabled;

    protected override ForecastEnsembleInput? Project(LocationForecast forecast) =>
        forecast is { Forecast: { } w, Ensemble: { } e } ? new(w, e) : null;

    protected override PipelineEvent? Evaluate(FeatureContext<ForecastEnsembleInput> ctx)
    {
        var thresholds = config.Value.Thresholds.ToSeverity(ScanDirection.Max);
        var window = ctx.Current.Forecast.NextHoursInclusive(ctx.Now);
        if (window.HighestBy(p => p.WetBulbTemperature) is not { WetBulbTemperature: { } peak } hottest)
        {
            return null;
        }

        var (severity, effectiveThreshold) = thresholds.Classify(peak);
        if (severity == AlertSeverity.None)
        {
            return null;
        }

        var rounded = Math.Round(peak, 1);
        var spread = ctx.Current.Ensemble.Points.SpreadAt(hottest.ValidFrom);
        var level = ConfidenceThresholds.Default.LevelFor(spread?.TemperatureSpread);
        return new WetBulbDangerAlertResult(
            ctx.Location, severity, level.ToConfidence(), rounded, effectiveThreshold,
            PeakValue: null, HoursUntil: hottest.HoursFrom(ctx.Now), DurationHours: null, Level: level,
            PeakWetBulb: rounded);
    }
}
