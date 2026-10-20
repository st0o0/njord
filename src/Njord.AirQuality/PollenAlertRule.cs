using Njord.Core.Configuration;
using Njord.Core.Domain;
using Njord.Forecast;
using Njord.Forecast.AirQuality;
using Njord.Messages.Alerts;

namespace Njord.AirQuality;

public sealed class PollenAlertRule : IAlertRule<DomainForecast<AirQualityPoint>>
{
    public const double YellowThreshold = 20.0;
    public const double OrangeThreshold = 50.0;
    public const double RedThreshold = 100.0;

    public AlertType Type => AlertType.Pollen;

    public Alert Evaluate(DomainForecast<AirQualityPoint> forecast, AlertOptions options, DateTimeOffset now)
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
            return Alert.None(Type);
        }

        var severity = AirQualityAlertWindow.SeverityFor(peak, YellowThreshold, OrangeThreshold, RedThreshold);
        if (severity == AlertSeverity.None)
        {
            return Alert.None(Type);
        }

        var threshold = severity switch
        {
            AlertSeverity.Red => RedThreshold,
            AlertSeverity.Orange => OrangeThreshold,
            _ => YellowThreshold,
        };
        return new Alert(Type, severity, 1.0, NoDetails.Instance, TriggerValue: Math.Round(peak, 1), Threshold: threshold, HoursUntil: peakPoint.HoursFrom(now), Level: ConfidenceLevel.High);
    }

    private static double HighestPollen(AirQualityPoint point) =>
        Math.Max(
            Math.Max(point.BirchPollen ?? 0, point.GrassPollen ?? 0),
            Math.Max(point.Alder ?? 0, point.Mugwort ?? 0));
}
