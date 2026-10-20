using Microsoft.Extensions.Options;
using Njord.Core.Domain;
using Njord.Features.Alerts.Shared;
using Njord.Forecast.Weather;
using Njord.Messages.Alerts;
using Njord.Messages.Pipeline;

namespace Njord.Features.Alerts.Storm;

public sealed class StormAlert(IOptions<StormConfig> config) : FeatureRule<WeatherInput>
{
    public override FeatureDescriptor Descriptor { get; } = new(
        OutputKind.Alert,
        FeatureCategory.Weather,
        DefaultEnabled: true,
        new DomainRequirements(Weather: WeatherParams.WindGusts | WeatherParams.EnsembleStdDev));

    public override bool Enabled => config.Value.Enabled;

    protected override WeatherInput? Project(LocationForecast forecast) =>
        forecast.Weather is { } w ? new(w) : null;

    protected override PipelineEvent? Evaluate(FeatureContext<WeatherInput> ctx)
    {
        var thresholds = SeverityThresholds.TryCreate(config.Value.GustThresholds, ScanDirection.Max, out var t)
            ? t
            : throw new InvalidOperationException("Invalid storm thresholds");
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
        var level = ConfidenceThresholds.Default.LevelFor(peak.EnsembleStdDev);
        return new StormAlertResult(
            ctx.Location, severity, level.ToConfidence(), rounded, effectiveThreshold,
            PeakValue: null, HoursUntil: peak.HoursFrom(ctx.Now), DurationHours: null, Level: level,
            ExpectedMaxGust: rounded);
    }
}
