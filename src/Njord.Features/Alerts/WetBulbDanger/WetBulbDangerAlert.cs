using Microsoft.Extensions.Options;
using Njord.Core.Domain;
using Njord.Features.Alerts.Shared;
using Njord.Forecast.Weather;
using Njord.Messages.Alerts;
using Njord.Messages.Pipeline;

namespace Njord.Features.Alerts.WetBulbDanger;

public sealed class WetBulbDangerAlert(IOptions<WetBulbDangerConfig> config) : FeatureRule<WeatherEnsembleInput>
{
    public override FeatureDescriptor Descriptor { get; } = new(
        OutputKind.Alert,
        FeatureCategory.Weather,
        Name: "WetBulbDanger",
        DefaultEnabled: false,
        new DomainRequirements(Weather: WeatherParams.WetBulbTemperature, Ensemble: EnsembleParams.TemperatureSpread));

    public override bool Enabled => config.Value.Enabled;

    protected override WeatherEnsembleInput? Project(LocationForecast forecast) =>
        forecast is { Weather: { } w, Ensemble: { } e } ? new(w, e) : null;

    protected override PipelineEvent? Evaluate(FeatureContext<WeatherEnsembleInput> ctx)
    {
        var thresholds = config.Value.EffectiveThresholds.ToSeverity(ScanDirection.Max);
        var window = ctx.Current.Weather.NextHoursInclusive(ctx.Now);
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
