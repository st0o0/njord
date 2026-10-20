using System.Collections.Immutable;
using Njord.Forecast;
using Njord.Forecast.Flood;
using Njord.Forecast.Weather;

namespace Njord.Flood.Tests;

internal static class FloodFixtures
{
    public static readonly DateTimeOffset Now = new(2026, 7, 11, 12, 0, 0, TimeSpan.Zero);
    public static readonly DateTimeOffset Today = new(2026, 7, 11, 0, 0, 0, TimeSpan.Zero);
    public static readonly Location Lucerne = new("lucerne", 47.05, 8.31);

    public static FloodPoint Point(int day, double discharge, double median = 100, double p75 = 150, double max = 400) =>
        new(Today.AddDays(day), discharge, DischargeMedian: median, DischargeP75: p75, DischargeMax: max);

    public static DomainForecast<FloodPoint> FloodForecast(params FloodPoint[] points) =>
        new(DomainKind.Flood, Now, TimeResolution.Daily, [.. points]);

    public static DomainForecast<WeatherPoint> Rain(double hourlyMillimeters)
    {
        var points = Enumerable.Range(0, 24)
            .Select(h => new WeatherPoint(Now.AddHours(h), 12, 12, 2, hourlyMillimeters, 80, 90, 1005, 61, 90, 10, 1))
            .ToImmutableArray();
        return new DomainForecast<WeatherPoint>(DomainKind.Weather, Now, TimeResolution.Hourly, points);
    }

}
