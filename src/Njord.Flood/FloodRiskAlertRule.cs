using Njord.Core.Configuration;
using Njord.Core.Domain;
using Njord.Messages.Alerts;
using Njord.Model;

namespace Njord.Flood;

public static class FloodAlertWindow
{
    public const int Days = 7;

    public static IReadOnlyList<FloodPoint> NextDays(this DomainForecast<FloodPoint> forecast, DateTimeOffset now)
    {
        var start = new DateTimeOffset(now.UtcDateTime.Date, TimeSpan.Zero);
        return ForecastQueries.Window(forecast, start, start.AddDays(Days));
    }

    public static IReadOnlyList<WeatherPoint> Next24Hours(this DomainForecast<WeatherPoint> forecast, DateTimeOffset now)
    {
        var start = AlertClock.HourStart(now);
        return ForecastQueries.Window(forecast, start, start.AddHours(24));
    }

    public static int HoursFrom(this FloodPoint point, DateTimeOffset now) =>
        Math.Max(0, (int)(point.ValidFrom - now).TotalHours);
}

public sealed class FloodRiskAlertRule : IAlertRule<DomainForecast<FloodPoint>>
{
    public const double MedianFactor = 2.0;

    public AlertType Type => AlertType.FloodRisk;

    public Alert Evaluate(DomainForecast<FloodPoint> forecast, AlertOptions options, DateTimeOffset now)
    {
        var best = Alert.None(Type);
        foreach (var point in forecast.NextDays(now))
        {
            var (severity, threshold) = Classify(point);
            if (severity > best.Severity
                || (severity == best.Severity && severity != AlertSeverity.None && point.Discharge > best.TriggerValue))
            {
                best = Build(Type, severity, Math.Round(point.Discharge, 1), Math.Round(threshold, 1), point.HoursFrom(now));
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
