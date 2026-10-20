using Microsoft.Extensions.Options;
using Njord.Core.Domain;
using Njord.Features.Alerts.Shared;
using Njord.Forecast.AirQuality;
using Njord.Messages.Alerts;
using Njord.Messages.Pipeline;

namespace Njord.Features.Alerts.Dust;

public sealed class DustAlert(IOptions<DustConfig> config) : FeatureRule<AqInput>
{
    public override FeatureDescriptor Descriptor { get; } = new(
        OutputKind.Alert,
        FeatureCategory.AirQuality,
        DefaultEnabled: false,
        new DomainRequirements(AirQuality: AirQualityParams.Dust));

    public override bool Enabled => config.Value.Enabled;

    protected override AqInput? Project(LocationForecast forecast) =>
        forecast.AirQuality is { } aq ? new(aq) : null;

    protected override PipelineEvent? Evaluate(FeatureContext<AqInput> ctx)
    {
        var thresholds = config.Value.EffectiveThresholds.ToSeverity(ScanDirection.Max);
        AirQualityPoint? peakPoint = null;
        var peak = 0.0;
        foreach (var point in ctx.Current.AirQuality.NextHoursInclusive(ctx.Now))
        {
            if (point.Dust is { } dust && dust > peak)
            {
                peak = dust;
                peakPoint = point;
            }
        }

        if (peakPoint is null)
        {
            return null;
        }

        var (severity, threshold) = thresholds.Classify(peak);
        if (severity == AlertSeverity.None)
        {
            return null;
        }

        return new DustAlertResult(
            ctx.Location, severity, 1.0, Math.Round(peak, 1), threshold,
            PeakValue: null, HoursUntil: peakPoint.HoursFrom(ctx.Now), DurationHours: null, Level: ConfidenceLevel.High);
    }
}
