using Njord.Core.Domain;
using Njord.Messages.Alerts;
using Njord.Model;

namespace Njord.Alerts.Flood;

public static class FloodAlertWindow
{
    public const int Days = 7;

    public static IReadOnlyList<FloodPoint> NextDays(this DomainForecast<FloodPoint> forecast, DateTimeOffset now)
    {
        var start = new DateTimeOffset(now.UtcDateTime.Date, TimeSpan.Zero);
        return ForecastQueries.Window(forecast, start, start.AddDays(Days));
    }

    public static int HoursFrom(this FloodPoint point, DateTimeOffset now) =>
        Math.Max(0, (int)(point.ValidFrom - now).TotalHours);
}

public sealed class FloodRiskAlertRule
{
    public const double MedianFactor = 2.0;

    public FloodAlert Evaluate(DomainForecast<FloodPoint> forecast, DateTimeOffset now)
    {
        var best = FloodAlert.None(FloodAlertType.FloodRisk);
        foreach (var point in forecast.NextDays(now))
        {
            var (severity, threshold) = Classify(point);
            if (severity > best.Severity
                || (severity == best.Severity && severity != AlertSeverity.None && point.Discharge > best.TriggerValue))
            {
                best = new FloodAlert(FloodAlertType.FloodRisk, severity, Math.Round(point.Discharge, 1), Math.Round(threshold, 1), point.HoursFrom(now));
            }
        }

        return best;
    }

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
