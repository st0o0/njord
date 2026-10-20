using Njord.Core.Configuration;
using Njord.Core.Domain;
using Njord.Forecast;
using Njord.Forecast.Flood;
using Njord.Forecast.Weather;
using Njord.Messages.Alerts;

namespace Njord.Flood;

public sealed class FloodRiskAlertRule : IAlertRule<DomainForecast<FloodPoint>>
{
    public const double MedianFactor = 2.0;

    public AlertType Type => AlertType.FloodRisk;

    public Alert Evaluate(DomainForecast<FloodPoint> forecast, AlertOptions options, DateTimeOffset now)
    {
        var best = Alert.None(Type);
        foreach (var point in forecast.NextDays(now))
        {
            if (point.Discharge is not { } discharge)
            {
                continue;
            }

            var (severity, threshold) = Classify(point);
            if (severity > best.Severity
                || (severity == best.Severity && severity != AlertSeverity.None && discharge > best.TriggerValue))
            {
                best = Build(Type, severity, Math.Round(discharge, 1), Math.Round(threshold, 1), point.HoursFrom(now));
            }
        }

        return best;
    }

    internal static Alert Build(AlertType type, AlertSeverity severity, double trigger, double threshold, int hoursUntil) =>
        new(type, severity, 1.0, NoDetails.Instance, TriggerValue: trigger, Threshold: threshold, HoursUntil: hoursUntil, Level: ConfidenceLevel.High);

    public static (AlertSeverity Severity, double Threshold) Classify(FloodPoint point)
    {
        if (point.DischargeMax is { } max && point.Discharge > max)
        {
            return (AlertSeverity.Red, max);
        }

        if (point.DischargeP75 is { } p75 && point.Discharge > p75)
        {
            return (AlertSeverity.Orange, p75);
        }

        if (point.DischargeMedian is { } median and > 0 && point.Discharge > median * MedianFactor)
        {
            return (AlertSeverity.Yellow, median * MedianFactor);
        }

        return (AlertSeverity.None, 0.0);
    }
}
