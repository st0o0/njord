using Microsoft.Extensions.Options;
using Njord.Core.Domain;
using Njord.Core.Forecast;
using Njord.Features.Alerts.Shared;
using Njord.Messages.Alerts;
using Njord.Messages.Pipeline;
using Njord.Messages.Results;
using Njord.Messages.Stream;

namespace Njord.Features.Alerts.ClearSkyUv;

public sealed class ClearSkyUvAlert(IOptions<ClearSkyUvConfig> config) : FeatureRule<ForecastInput>
{
    public override FeatureDescriptor Descriptor { get; } = new(
        OutputKind.Alert,
        FeatureCategory.Lifestyle,
        Name: "ClearSkyUv",
        DefaultEnabled: false,
        new DomainRequirements(Forecast: ForecastParams.UvIndexClearSky | ForecastParams.IsDay));

    public override bool Enabled => config.Value.Enabled;

    protected override ForecastInput? Project(LocationForecast forecast) =>
        forecast.Forecast is { } w ? new(w) : null;

    protected override PipelineEvent? Evaluate(FeatureContext<ForecastInput> ctx)
    {
        var thresholds = config.Value.EffectiveThresholds.ToSeverity(ScanDirection.Max);
        var daytime = ctx.Current.Forecast.NextHoursInclusive(ctx.Now)
            .Where(p => p.IsDay is > 0)
            .ToList();
        if (daytime.HighestBy(p => p.UvIndexClearSky) is not { UvIndexClearSky: { } peak } highest)
        {
            return null;
        }

        var (severity, effectiveThreshold) = thresholds.Classify(peak);
        if (severity == AlertSeverity.None)
        {
            return null;
        }

        var rounded = Math.Round(peak, 1);
        var level = ConfidenceLevel.Medium;
        return new ClearSkyUvAlertResult(
            ctx.Location, severity, level.ToConfidence(), rounded, effectiveThreshold,
            PeakValue: null, HoursUntil: highest.HoursFrom(ctx.Now), DurationHours: null, Level: level,
            PeakClearSkyUv: rounded);
    }
}
