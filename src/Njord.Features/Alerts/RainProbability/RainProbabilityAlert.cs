using Microsoft.Extensions.Options;
using Njord.Core.Domain;
using Njord.Features.Alerts.Shared;
using Njord.Forecast.Weather;
using Njord.Messages.Alerts;
using Njord.Messages.Pipeline;

namespace Njord.Features.Alerts.RainProbability;

public sealed class RainProbabilityAlert(IOptions<RainProbabilityConfig> config) : FeatureRule<WeatherEnsembleInput>
{
    public override FeatureDescriptor Descriptor { get; } = new(
        OutputKind.Alert,
        FeatureCategory.Weather,
        Name: "RainProbability",
        DefaultEnabled: true,
        new DomainRequirements(Weather: WeatherParams.PrecipitationProbability, Ensemble: EnsembleParams.TemperatureSpread));

    public override bool Enabled => config.Value.Enabled;

    protected override WeatherEnsembleInput? Project(LocationForecast forecast) =>
        forecast is { Weather: { } w, Ensemble: { } e } ? new(w, e) : null;

    protected override PipelineEvent? Evaluate(FeatureContext<WeatherEnsembleInput> ctx)
    {
        var thresholds = config.Value.EffectiveThresholds.ToSeverity(ScanDirection.Max);
        var window = ctx.Current.Weather.NextHoursInclusive(ctx.Now, config.Value.WindowHours);
        if (window.HighestBy(p => p.PrecipitationProbability) is not { PrecipitationProbability: { } peak } highest)
        {
            return null;
        }

        var (severity, effectiveThreshold) = thresholds.Classify(peak);
        if (severity == AlertSeverity.None)
        {
            return null;
        }

        var rounded = Math.Round(peak, 1);
        var spread = ctx.Current.Ensemble.Points.SpreadAt(highest.ValidFrom);
        var level = ConfidenceThresholds.Default.LevelFor(spread?.TemperatureSpread);
        return new RainProbabilityAlertResult(
            ctx.Location, severity, level.ToConfidence(), rounded, effectiveThreshold,
            PeakValue: null, HoursUntil: highest.HoursFrom(ctx.Now), DurationHours: null, Level: level,
            PeakProbability: rounded);
    }
}
