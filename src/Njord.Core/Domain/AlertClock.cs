namespace Njord.Core.Domain;

public static class AlertClock
{
    public static DateTimeOffset HourStart(DateTimeOffset now)
    {
        var utc = now.UtcDateTime;
        return new DateTimeOffset(utc.Date.AddHours(utc.Hour), TimeSpan.Zero);
    }
}
