using Njord.OpenMeteo;

namespace Njord.Tests.Shared;

public sealed class FakeFloodClient : IFloodClient
{
    private static readonly long StartUnix = new DateTimeOffset(2026, 7, 11, 0, 0, 0, TimeSpan.Zero).ToUnixTimeSeconds();

    public IReadOnlyList<string> LastVariables { get; private set; } = [];

    public int LastForecastDays { get; private set; }

    public bool Fails { get; set; }

    private int _calls;

    public int Calls => Volatile.Read(ref _calls);

    public Task<FloodResponse?> FetchAsync(double latitude, double longitude, IReadOnlyList<string> variables, int forecastDays, CancellationToken cancellationToken)
    {
        LastVariables = variables;
        LastForecastDays = forecastDays;
        Interlocked.Increment(ref _calls);

        if (Fails)
        {
            return Task.FromResult<FloodResponse?>(null);
        }

        return Task.FromResult<FloodResponse?>(new FloodResponse(latitude, longitude,
            new FloodDailyData(
                Time: [StartUnix],
                Discharge: [120.0],
                DischargeMedian: [100.0],
                DischargeP75: [140.0])));
    }
}
