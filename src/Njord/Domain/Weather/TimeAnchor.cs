namespace Njord.Domain.Weather;

public static class TimeAnchor
{
    public static DateTimeOffset AtHorizon(DateTimeOffset tick, int horizonHours)
    {
        var target = tick.AddHours(horizonHours);
        var floored = new DateTimeOffset(target.Year, target.Month, target.Day, target.Hour, 0, 0, target.Offset);
        return floored == target ? target : floored.AddHours(1);
    }
}
