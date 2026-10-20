using Njord.Model;

namespace Njord.Marine.Tests;

internal static class MarineFixtures
{
    public static readonly DateTimeOffset Now = new(2026, 7, 11, 12, 0, 0, TimeSpan.Zero);
    public static readonly Location Lucerne = new("lucerne", 47.05, 8.31);

    public static MarinePoint Point(int hour, double waveHeight = 0.3, double? waterTemperature = 20.0) =>
        new(Now.AddHours(hour), waveHeight, 4.0, 270.0, waterTemperature);

    public static DomainForecast<MarinePoint> Forecast(params MarinePoint[] points) =>
        new(DomainKind.Marine, Now, TimeResolution.Hourly, [.. points]);
}
