using Microsoft.Extensions.Options;
using Njord.Core.Domain;
using Njord.Features.Alerts.Shared;
using Njord.Forecast.Weather;
using Njord.Messages.Alerts;
using Njord.Messages.Pipeline;

namespace Njord.Features.Alerts.Visibility;

public sealed class VisibilityAlert(IOptions<VisibilityConfig> config) : FeatureRule<WeatherInput>
{
    public override FeatureDescriptor Descriptor { get; } = new(
        OutputKind.Alert,
        FeatureCategory.Weather,
        DefaultEnabled: true,
        new DomainRequirements(Weather: WeatherParams.Visibility | WeatherParams.EnsembleStdDev));

    public override bool Enabled => config.Value.Enabled;

    protected override WeatherInput? Project(LocationForecast forecast) =>
        forecast.Weather is { } w ? new(w) : null;

    protected override PipelineEvent? Evaluate(FeatureContext<WeatherInput> ctx)
    {
        var thresholds = SeverityThresholds.TryCreate(config.Value.Thresholds, ScanDirection.Min, out var t)
            ? t
            : throw new InvalidOperationException("Invalid visibility thresholds");
        var window = ctx.Current.Weather.NextHoursInclusive(ctx.Now);
        if (window.LowestBy(p => p.Visibility) is not { Visibility: { } low } lowest)
        {
            return null;
        }

        var hoursBelow = window.Count(p => p.Visibility is { } v && thresholds.CrossesAdvisory(v, strict: true));
        var (severity, effectiveThreshold) = thresholds.Classify(low, strict: true);
        if (severity == AlertSeverity.None)
        {
            return null;
        }

        var rounded = Math.Round(low, 0);
        var level = ConfidenceThresholds.Default.LevelFor(lowest.EnsembleStdDev);
        return new VisibilityAlertResult(
            ctx.Location, severity, level.ToConfidence(), rounded, effectiveThreshold,
            PeakValue: null, HoursUntil: lowest.HoursFrom(ctx.Now), DurationHours: null, Level: level,
            MinVisibility: rounded, HoursBelow1000m: hoursBelow);
    }
}
