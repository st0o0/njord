using Njord.Forecast.Weather;
using Njord.Messages.Alerts;

namespace Njord.Features.Alerts.Shared;

internal static class AlertWindow
{
    public static Alert Build(
        AlertSeverity severity,
        WeatherPoint? peak,
        AlertDetails details,
        double triggerValue,
        double threshold,
        double? peakValue = null,
        int? hoursUntil = null,
        int? durationHours = null)
    {
        var level = ConfidenceThresholds.Default.LevelFor(peak?.EnsembleStdDev);
        return new Alert(severity, level.ToConfidence(), details,
            TriggerValue: triggerValue,
            Threshold: threshold,
            PeakValue: peakValue,
            HoursUntil: hoursUntil,
            DurationHours: durationHours,
            Level: level);
    }

    public static WeatherPoint? LowestBy(this IEnumerable<WeatherPoint> points, Func<WeatherPoint, double?> selector)
    {
        WeatherPoint? best = null;
        var bestValue = double.MaxValue;
        foreach (var point in points)
        {
            if (selector(point) is { } value && value < bestValue)
            {
                best = point;
                bestValue = value;
            }
        }

        return best;
    }

    public static WeatherPoint? HighestBy(this IEnumerable<WeatherPoint> points, Func<WeatherPoint, double?> selector)
    {
        WeatherPoint? best = null;
        var bestValue = double.MinValue;
        foreach (var point in points)
        {
            if (selector(point) is { } value && value > bestValue)
            {
                best = point;
                bestValue = value;
            }
        }

        return best;
    }
}
