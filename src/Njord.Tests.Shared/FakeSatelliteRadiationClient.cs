using Njord.OpenMeteo;

namespace Njord.Tests.Shared;

public sealed class FakeSatelliteRadiationClient : ISatelliteRadiationClient
{
    private static readonly long[] Hours =
    [
        new DateTimeOffset(2026, 7, 1, 8, 0, 0, TimeSpan.Zero).ToUnixTimeSeconds(),
        new DateTimeOffset(2026, 7, 1, 9, 0, 0, TimeSpan.Zero).ToUnixTimeSeconds(),
        new DateTimeOffset(2026, 7, 1, 10, 0, 0, TimeSpan.Zero).ToUnixTimeSeconds(),
    ];

    public IReadOnlyList<string> LastVariables { get; private set; } = [];

    public int LastPastHours { get; private set; }

    public bool Fails { get; set; }

    private int _calls;

    public int Calls => Volatile.Read(ref _calls);

    public Task<SatelliteRadiationResponse?> FetchAsync(double latitude, double longitude, IReadOnlyList<string> variables, int pastHours, CancellationToken cancellationToken)
    {
        LastVariables = variables;
        LastPastHours = pastHours;
        Interlocked.Increment(ref _calls);

        return Task.FromResult<SatelliteRadiationResponse?>(Fails
            ? null
            : new SatelliteRadiationResponse(latitude, longitude, new SatelliteRadiationHourlyData(
                Hours,
                [300.0, 450.0, 520.0],
                [200.0, 300.0, 350.0],
                [100.0, 150.0, 170.0],
                [400.0, 500.0, 560.0],
                [350.0, 480.0, 540.0])));
    }
}
