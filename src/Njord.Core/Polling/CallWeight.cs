namespace Njord.Core.Polling;

public static class CallWeight
{
    public static double Of(int hourlyVariableCount, int forecastDays) =>
        Math.Ceiling(hourlyVariableCount / 10.0) * (forecastDays / 14.0);
}
