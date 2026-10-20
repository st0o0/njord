using Njord.OpenMeteo;

namespace Njord.Tests.Shared;

public sealed class FakeSeasonalClient : ISeasonalClient
{
    private static readonly long[] Months =
    [
        new DateTimeOffset(2026, 7, 1, 0, 0, 0, TimeSpan.Zero).ToUnixTimeSeconds(),
        new DateTimeOffset(2026, 8, 1, 0, 0, 0, TimeSpan.Zero).ToUnixTimeSeconds(),
        new DateTimeOffset(2026, 9, 1, 0, 0, 0, TimeSpan.Zero).ToUnixTimeSeconds(),
        new DateTimeOffset(2026, 10, 1, 0, 0, 0, TimeSpan.Zero).ToUnixTimeSeconds(),
        new DateTimeOffset(2026, 11, 1, 0, 0, 0, TimeSpan.Zero).ToUnixTimeSeconds(),
    ];

    public IReadOnlyList<string> LastVariables { get; private set; } = [];

    public bool Fails { get; set; }

    private int _calls;

    public int Calls => Volatile.Read(ref _calls);

    public Task<SeasonalResponse?> FetchAsync(double latitude, double longitude, IReadOnlyList<string> variables, CancellationToken cancellationToken)
    {
        LastVariables = variables;
        Interlocked.Increment(ref _calls);

        return Task.FromResult<SeasonalResponse?>(Fails
            ? null
            : new SeasonalResponse(latitude, longitude, new SeasonalMonthlyData(
                Months,
                [1.0, 1.5, 0.5, 2.0, 1.0],
                [5.0, -10.0, 0.0, 4.0, 8.0],
                [80.0, 90.0, 100.0, 100.0, 90.0])));
    }
}
