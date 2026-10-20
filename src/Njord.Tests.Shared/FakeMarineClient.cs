using Njord.OpenMeteo;

namespace Njord.Tests.Shared;

public sealed class FakeMarineClient : IMarineClient
{
    private static readonly long StartUnix = new DateTimeOffset(2026, 7, 11, 0, 0, 0, TimeSpan.Zero).ToUnixTimeSeconds();

    public IReadOnlyList<string> LastVariables { get; private set; } = [];

    public int LastForecastDays { get; private set; }

    public bool Fails { get; set; }

    private int _calls;

    public int Calls => Volatile.Read(ref _calls);

    public Task<MarineResponse?> FetchAsync(double latitude, double longitude, IReadOnlyList<string> variables, int forecastDays, CancellationToken cancellationToken)
    {
        LastVariables = variables;
        LastForecastDays = forecastDays;
        Interlocked.Increment(ref _calls);

        if (Fails)
        {
            return Task.FromResult<MarineResponse?>(null);
        }

        return Task.FromResult<MarineResponse?>(new MarineResponse(latitude, longitude,
            new MarineHourlyData(
                Time: [StartUnix],
                WaveHeight: [0.4],
                WavePeriod: [4.0],
                WaveDirection: [270.0],
                SeaSurfaceTemperature: [19.0])));
    }
}
