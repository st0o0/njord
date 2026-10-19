using Njord.Core.Domain;
using Njord.Messages.Alerts;
using Njord.Model;

namespace Njord.AirQuality;

internal static class AirQualityAlertWindow
{
    public const int Hours = 24;

    public static IReadOnlyList<AirQualityPoint> Next24Hours(this DomainForecast<AirQualityPoint> forecast, DateTimeOffset now)
    {
        var start = AlertClock.HourStart(now);
        return ForecastQueries.Window(forecast, start, start.AddHours(Hours));
    }

    public static int HoursFrom(this AirQualityPoint point, DateTimeOffset now) =>
        (int)(point.ValidFrom - AlertClock.HourStart(now)).TotalHours;

    public static AlertSeverity SeverityFor(double value, double yellow, double orange, double red) => value switch
    {
        _ when value >= red => AlertSeverity.Red,
        _ when value >= orange => AlertSeverity.Orange,
        _ when value >= yellow => AlertSeverity.Yellow,
        _ => AlertSeverity.None,
    };
}
