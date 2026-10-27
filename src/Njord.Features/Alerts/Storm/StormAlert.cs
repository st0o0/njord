using Microsoft.Extensions.Options;
using Njord.Core.Domain;
using Njord.Features.Alerts.Shared;
using Njord.Forecast.Weather;
using Njord.Messages.Alerts;
using Njord.Messages.Pipeline;

namespace Njord.Features.Alerts.Storm;

public sealed class StormAlert(IOptions<StormConfig> config) : FeatureRule<WeatherEnsembleInput>
{
    public override FeatureDescriptor Descriptor { get; } = new(
        OutputKind.Alert,
        FeatureCategory.Weather,
        Name: "Storm",
        DefaultEnabled: true,
        new DomainRequirements(Weather: WeatherParams.WindGusts, Ensemble: EnsembleParams.TemperatureSpread));

    public override bool Enabled => config.Value.Enabled;

    protected override WeatherEnsembleInput? Project(LocationForecast forecast) =>
        forecast is { Weather: { } w, Ensemble: { } e } ? new(w, e) : null;

    protected override PipelineEvent? Evaluate(FeatureContext<WeatherEnsembleInput> ctx)
    {
        var thresholds = config.Value.EffectiveGustThresholds.ToSeverity(ScanDirection.Max);
        if (ctx.Current.Weather.NextHoursInclusive(ctx.Now).HighestBy(p => p.WindGusts) is not { WindGusts: { } gust } peak)
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
        var level = ConfidenceThresholds.Default.LevelFor(spread?.TemperatureSpread);
        return new StormAlertResult(
            ctx.Location, severity, level.ToConfidence(), rounded, effectiveThreshold,
            PeakValue: null, HoursUntil: peak.HoursFrom(ctx.Now), DurationHours: null, Level: level,
            ExpectedMaxGust: rounded);
    }
}
