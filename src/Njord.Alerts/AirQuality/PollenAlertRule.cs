using Njord.Messages.Alerts;
using Njord.Model;

namespace Njord.Alerts.AirQuality;

public sealed class PollenAlertRule : IAirQualityAlertRule
{
    public const double YellowThreshold = 20.0;
    public const double OrangeThreshold = 50.0;
    public const double RedThreshold = 100.0;

    public AirQualityAlertType Type => AirQualityAlertType.Pollen;

    public AirQualityAlert Evaluate(DomainForecast<AirQualityPoint> forecast, DateTimeOffset now)
    {
        AirQualityPoint? peakPoint = null;
        var peak = 0.0;
        foreach (var point in forecast.Next24Hours(now))
        {
            var value = HighestPollen(point);
            if (value > peak)
            {
                peak = value;
                peakPoint = point;
            }
        }

        if (peakPoint is null)
        {
            return AirQualityAlert.None(Type);
        }

        var severity = AirQualityAlertWindow.SeverityFor(peak, YellowThreshold, OrangeThreshold, RedThreshold);
        if (severity == AlertSeverity.None)
        {
            return AirQualityAlert.None(Type);
        }

        var threshold = severity switch
        {
            AlertSeverity.Red => RedThreshold,
            AlertSeverity.Orange => OrangeThreshold,
            _ => YellowThreshold,
        };
        return new AirQualityAlert(Type, severity, Math.Round(peak, 1), threshold, peakPoint.HoursFrom(now));
    }

    private static double HighestPollen(AirQualityPoint point) =>
        Math.Max(
            Math.Max(point.BirchPollen ?? 0, point.GrassPollen ?? 0),
            Math.Max(point.Alder ?? 0, point.Mugwort ?? 0));
}
