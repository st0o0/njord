namespace Njord.Forecast;

public static class TimeResolutionExtensions
{
    public static TimeSpan ToTimeSpan(this TimeResolution resolution) => resolution switch
    {
        TimeResolution.QuarterHourly => TimeSpan.FromMinutes(15),
        TimeResolution.Hourly => TimeSpan.FromHours(1),
        TimeResolution.ThreeHourly => TimeSpan.FromHours(3),
        TimeResolution.SixHourly => TimeSpan.FromHours(6),
        TimeResolution.Daily => TimeSpan.FromDays(1),
        TimeResolution.Monthly => TimeSpan.FromDays(30),
        _ => throw new ArgumentOutOfRangeException(nameof(resolution), resolution, null)
    };
}
