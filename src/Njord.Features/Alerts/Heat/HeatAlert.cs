using Microsoft.Extensions.Options;
using Njord.Core.Domain;
using Njord.Features.Alerts.Shared;
using Njord.Forecast.Weather;
using Njord.Messages.Alerts;
using Njord.Messages.Pipeline;

namespace Njord.Features.Alerts.Heat;

public sealed class HeatAlert(IOptions<HeatConfig> config) : FeatureRule<WeatherEnsembleInput>
{
    public override FeatureDescriptor Descriptor { get; } = new(
        OutputKind.Alert,
        FeatureCategory.Weather,
        Name: "Heat",
        DefaultEnabled: true,
        new DomainRequirements(Weather: WeatherParams.Temperature | WeatherParams.ApparentTemperature, Ensemble: EnsembleParams.TemperatureSpread));

    public override bool Enabled => config.Value.Enabled;

    protected override WeatherEnsembleInput? Project(LocationForecast forecast) =>
        forecast is { Weather: { } w, Ensemble: { } e } ? new(w, e) : null;

    protected override PipelineEvent? Evaluate(FeatureContext<WeatherEnsembleInput> ctx)
    {
        var thresholds = config.Value.EffectiveThresholds.ToSeverity(ScanDirection.Max);
        var window = ctx.Current.Weather.NextHoursInclusive(ctx.Now);
        if (window.HighestBy(p => p.ApparentTemperature) is not { ApparentTemperature: { } apparent } hottestApparent
            || window.HighestBy(p => p.Temperature) is not { Temperature: { } air } hottestAir)
        {
            return null;
        }

        var (peak, max) = apparent >= air
            ? (hottestApparent, apparent)
            : (hottestAir, air);

        var (severity, effectiveThreshold) = thresholds.Classify(max);
        if (severity == AlertSeverity.None)
        {
            return null;
        }

        var rounded = Math.Round(max, 1);
        var spread = ctx.Current.Ensemble.Points.SpreadAt(peak.ValidFrom);
        var level = ConfidenceThresholds.Default.LevelFor(spread?.TemperatureSpread);
        return new HeatAlertResult(
            ctx.Location, severity, level.ToConfidence(), rounded, effectiveThreshold,
            PeakValue: null, HoursUntil: peak.HoursFrom(ctx.Now), DurationHours: null, Level: level,
            ExpectedMax: rounded);
    }
}
