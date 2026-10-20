using Njord.OpenMeteo;

namespace Njord.Tests.Shared;

public sealed class FakeAirQualityClient : IAirQualityClient
{
    private static readonly long StartUnix = new DateTimeOffset(2026, 7, 11, 3, 0, 0, TimeSpan.Zero).ToUnixTimeSeconds();

    public IReadOnlyList<string> LastVariables { get; private set; } = [];

    public int LastForecastDays { get; private set; }

    public bool Fails { get; set; }

    private int _calls;

    public int Calls => Volatile.Read(ref _calls);

    public Task<AirQualityResponse?> FetchAsync(double latitude, double longitude, IReadOnlyList<string> variables, int forecastDays, CancellationToken cancellationToken)
    {
        LastVariables = variables;
        LastForecastDays = forecastDays;
        Interlocked.Increment(ref _calls);

        if (Fails)
        {
            return Task.FromResult<AirQualityResponse?>(null);
        }

        return Task.FromResult<AirQualityResponse?>(new AirQualityResponse(latitude, longitude,
            new AirQualityHourlyData(
                Time: [StartUnix],
                Pm25: [8.0],
                Pm10: [15.0],
                Ozone: [60.0])));
    }
}
