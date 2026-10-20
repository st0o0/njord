using Njord.Forecast;

namespace Njord.Core.Domain;

public static class ForecastWindow
{
    public const int DefaultHours = 24;

    // Half-open [hourStart, hourStart + hours): exactly `hours` points on hourly data.
    public static IReadOnlyList<T> NextHours<T>(this DomainForecast<T> forecast, DateTimeOffset now, int hours = DefaultHours)
        where T : IHasValidTime
        => ForecastQueries.Upcoming(forecast, now, hours);

    // Closed [hourStart, hourStart + hours]: alert rules keep the end boundary (25 hourly points for 24 h).
    public static IReadOnlyList<T> NextHoursInclusive<T>(this DomainForecast<T> forecast, DateTimeOffset now, int hours = DefaultHours)
        where T : IHasValidTime
    {
        var start = AlertClock.HourStart(now);
        return ForecastQueries.Window(forecast, start, start.AddHours(hours));
    }

    // Closed [UTC midnight, midnight + days] for daily data.
    public static IReadOnlyList<T> NextDays<T>(this DomainForecast<T> forecast, DateTimeOffset now, int days)
        where T : IHasValidTime
    {
        var start = new DateTimeOffset(now.UtcDateTime.Date, TimeSpan.Zero);
        return ForecastQueries.Window(forecast, start, start.AddDays(days));
    }

    public static int HoursFrom(this IHasValidTime point, DateTimeOffset now) =>
        Math.Max(0, (int)(point.ValidFrom - AlertClock.HourStart(now)).TotalHours);
}
