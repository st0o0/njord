namespace Njord.Core.Polling;

public static class CallWeight
{
    public static int Of(int hourlyVariableCount, int forecastDays) =>
        (int)Math.Ceiling(hourlyVariableCount / 10.0) * (int)Math.Ceiling(forecastDays / 14.0);
}
