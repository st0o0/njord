using Njord.Core.Configuration;
using Njord.Core.Domain;
using Njord.Forecast;
using Njord.Forecast.Flood;
using Njord.Forecast.Weather;
using Njord.Messages.Alerts;

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
