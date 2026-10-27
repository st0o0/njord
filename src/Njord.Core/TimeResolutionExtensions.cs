namespace Njord.Core;

public static class TimeResolutionExtensions
{
    public static TimeSpan ToTimeSpan(this TimeResolution resolution) => resolution switch
    {
        TimeResolution.Hourly => TimeSpan.FromHours(1),
        TimeResolution.Daily => TimeSpan.FromDays(1),
        TimeResolution.Monthly => TimeSpan.FromDays(30),
        _ => throw new ArgumentOutOfRangeException(nameof(resolution), resolution, null)
    };
}
