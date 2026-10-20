using Microsoft.Extensions.Options;
using Njord.Core.Domain;
using Njord.Features.Alerts.Shared;
using Njord.Forecast.AirQuality;
using Njord.Messages.Alerts;
using Njord.Messages.Pipeline;

namespace Njord.Features.Alerts.Pm25;

public sealed class Pm25Alert(IOptions<Pm25Config> config) : FeatureRule<AqInput>
{
    public const double WhoGuideline = 15.0;
    public const double OrangeThreshold = 25.0;
    public const double RedThreshold = 50.0;

    public override FeatureDescriptor Descriptor { get; } = new(
        OutputKind.Alert,
        FeatureCategory.AirQuality,
        DefaultEnabled: true,
        new DomainRequirements(AirQuality: AirQualityParams.Pm25));

    public override bool Enabled => config.Value.Enabled;

    protected override AqInput? Project(LocationForecast forecast) =>
        forecast.AirQuality is { } aq ? new(aq) : null;

    protected override PipelineEvent? Evaluate(FeatureContext<AqInput> ctx)
    {
        AirQualityPoint? peakPoint = null;
        foreach (var point in ctx.Current.AirQuality.NextHoursInclusive(ctx.Now))
        {
            if (point.Pm25 is { } pm25 && (peakPoint?.Pm25 is not { } best || pm25 > best))
            {
                peakPoint = point;
            }
        }

        if (peakPoint?.Pm25 is not { } peakPm25)
        {
            return null;
        }

        var severity = AirQualityAlertWindow.SeverityFor(peakPm25, WhoGuideline, OrangeThreshold, RedThreshold);
        if (severity == AlertSeverity.None)
        {
            return null;
        }

        var threshold = severity switch
        {
            AlertSeverity.Red => RedThreshold,
            AlertSeverity.Orange => OrangeThreshold,
            _ => WhoGuideline,
        };
        return new Pm25AlertResult(
            ctx.Location, severity, 1.0, Math.Round(peakPm25, 1), threshold,
            PeakValue: null, HoursUntil: peakPoint.HoursFrom(ctx.Now), DurationHours: null, Level: ConfidenceLevel.High);
    }
}
