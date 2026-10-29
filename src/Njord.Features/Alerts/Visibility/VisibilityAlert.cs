using Microsoft.Extensions.Options;
using Njord.Core.Domain;
using Njord.Core.Forecast;
using Njord.Features.Alerts.Shared;
using Njord.Messages.Alerts;
using Njord.Messages.Results;
using Njord.Messages.Results.Alerts;
using Njord.Messages.Stream;

namespace Njord.Features.Alerts.Visibility;

public sealed class VisibilityAlert(IOptions<VisibilityConfig> config) : FeatureRule<ForecastInput>
{
    public override FeatureDescriptor Descriptor { get; } = new(
        OutputKind.Alert,
        FeatureCategory.Forecast,
        Name: "Visibility",
        DefaultEnabled: true,
        new DomainRequirements(Forecast: ForecastParams.Visibility));

    public override bool Enabled => config.Value.Enabled;

    protected override ForecastInput? Project(LocationForecast forecast) =>
        forecast.Forecast is { } w ? new(w) : null;

    protected override PipelineEvent? Evaluate(FeatureContext<ForecastInput> ctx)
    {
        var thresholds = config.Value.Thresholds.ToSeverity(ScanDirection.Min);
        var window = ctx.Current.Forecast.NextHoursInclusive(ctx.Now);
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
        var level = ConfidenceLevel.Medium;
        return new VisibilityAlertResult(
            ctx.Location, severity, level.ToConfidence(), rounded, effectiveThreshold,
            PeakValue: null, HoursUntil: lowest.HoursFrom(ctx.Now), DurationHours: null, Level: level,
            MinVisibility: rounded, HoursBelow1000m: hoursBelow);
    }
}
