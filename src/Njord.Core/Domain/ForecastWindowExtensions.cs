using Njord.Core;

namespace Njord.Core.Domain;

public static class ForecastWindowExtensions
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
        var start = now.HourStart();
        return ForecastQueries.Window(forecast, start, start.AddHours(hours));
    }

    // Closed [UTC midnight, midnight + days] for daily data.
    public static IReadOnlyList<T> NextDays<T>(this DomainForecast<T> forecast, DateTimeOffset now, int days)
        where T : IHasValidTime
    {
        var start = new DateTimeOffset(now.UtcDateTime.Date, TimeSpan.Zero);
        return ForecastQueries.Window(forecast, start, start.AddDays(days));
    }

    public static DateTimeOffset HourStart(this DateTimeOffset now)
    {
        var utc = now.UtcDateTime;
        return new DateTimeOffset(utc.Date.AddHours(utc.Hour), TimeSpan.Zero);
    }

    public static int HoursFrom(this IHasValidTime point, DateTimeOffset now) =>
        Math.Max(0, (int)(point.ValidFrom - now.HourStart()).TotalHours);
}
