using Njord.Core.Domain;
using Njord.Messages.Alerts;
using Njord.Model;

namespace Njord.Alerts.AirQuality;

public sealed class Pm25AlertRule : IAirQualityAlertRule
{
    public const double WhoGuideline = 15.0;
    public const double OrangeThreshold = 25.0;
    public const double RedThreshold = 50.0;

    public AirQualityAlertType Type => AirQualityAlertType.Pm25;

    public AirQualityAlert Evaluate(DomainForecast<AirQualityPoint> forecast, DateTimeOffset now)
    {
        AirQualityPoint? peakPoint = null;
        foreach (var point in forecast.Next24Hours(now))
        {
            if (peakPoint is null || point.Pm25 > peakPoint.Pm25)
            {
                peakPoint = point;
            }
        }

        if (peakPoint is null)
        {
            return AirQualityAlert.None(Type);
        }

        var severity = AirQualityAlertWindow.SeverityFor(peakPoint.Pm25, WhoGuideline, OrangeThreshold, RedThreshold);
        if (severity == AlertSeverity.None)
        {
            return AirQualityAlert.None(Type);
        }

        var threshold = severity switch
        {
            AlertSeverity.Red => RedThreshold,
            AlertSeverity.Orange => OrangeThreshold,
            _ => WhoGuideline,
        };
        return new AirQualityAlert(Type, severity, Math.Round(peakPoint.Pm25, 1), threshold, peakPoint.HoursFrom(now));
    }
}
