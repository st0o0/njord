using Njord.Core.Domain;
using Njord.Messages.Alerts;
using Njord.Model;

namespace Njord.Alerts;

internal static class AlertWindow
{
    public const int Hours = 24;

    public static DateTimeOffset Start(DateTimeOffset now)
    {
        var utc = now.UtcDateTime;
        return new DateTimeOffset(utc.Date.AddHours(utc.Hour), TimeSpan.Zero);
    }

    public static IReadOnlyList<WeatherPoint> Next24Hours(this DomainForecast<WeatherPoint> forecast, DateTimeOffset now)
    {
        var start = Start(now);
        return ForecastQueries.Window(forecast, start, start.AddHours(Hours));
    }

    public static int HoursFrom(this WeatherPoint point, DateTimeOffset now) =>
        (int)(point.ValidFrom - Start(now)).TotalHours;

    public static Alert Build(
        this AlertSettings settings,
        AlertType type,
        AlertSeverity severity,
        WeatherPoint? peak,
        AlertDetails details,
        double triggerValue,
        double threshold,
        double? peakValue = null,
        int? hoursUntil = null,
        int? durationHours = null)
    {
        var level = settings.Confidence.LevelFor(peak?.EnsembleStdDev);
        return new Alert(type, severity, level.ToConfidence(), details,
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
