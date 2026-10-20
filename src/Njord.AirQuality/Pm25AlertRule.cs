using Njord.Core.Configuration;
using Njord.Core.Domain;
using Njord.Forecast;
using Njord.Forecast.AirQuality;
using Njord.Messages.Alerts;

namespace Njord.AirQuality;

public sealed class Pm25AlertRule : IAlertRule<DomainForecast<AirQualityPoint>>
{
    public const double WhoGuideline = 15.0;
    public const double OrangeThreshold = 25.0;
    public const double RedThreshold = 50.0;

    public AlertType Type => AlertType.Pm25;

    public FeatureDescriptor Feature { get; } = new(
        FeatureKind.Pm25Alert,
        FeatureCategory.AirQuality,
        "Fine particulate warning against the WHO guideline",
        DefaultEnabled: true,
        new DomainRequirements(AirQuality: AirQualityParams.Pm25));

    public Alert Evaluate(DomainForecast<AirQualityPoint> forecast, AlertOptions options, DateTimeOffset now)
    {
        AirQualityPoint? peakPoint = null;
        foreach (var point in forecast.NextHoursInclusive(now))
        {
            if (point.Pm25 is { } pm25 && (peakPoint?.Pm25 is not { } best || pm25 > best))
            {
                peakPoint = point;
            }
        }

        if (peakPoint?.Pm25 is not { } peakPm25)
        {
            return Alert.None(Type);
        }

        var severity = AirQualityAlertWindow.SeverityFor(peakPm25, WhoGuideline, OrangeThreshold, RedThreshold);
        if (severity == AlertSeverity.None)
        {
            return Alert.None(Type);
        }

        var threshold = severity switch
        {
            AlertSeverity.Red => RedThreshold,
            AlertSeverity.Orange => OrangeThreshold,
            _ => WhoGuideline,
        };
        return new Alert(Type, severity, 1.0, NoDetails.Instance, TriggerValue: Math.Round(peakPm25, 1), Threshold: threshold, HoursUntil: peakPoint.HoursFrom(now), Level: ConfidenceLevel.High);
    }
}
