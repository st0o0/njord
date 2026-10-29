using Njord.Core;
using Njord.Core.Forecast;
using Njord.Core.Marine;
using Njord.Messages.Fetch;

namespace Njord.Tests.Shared;

public sealed class FakeMarineClient : IDomainClient
{
    public DomainKind Domain => DomainKind.Marine;

    public int CallWeight => 1;

    public bool Fails { get; set; }

    private int _calls;

    public int Calls => Volatile.Read(ref _calls);

    public Task<FetchResult> FetchAsync(double latitude, double longitude, DateTimeOffset now, CancellationToken cancellationToken)
    {
        Interlocked.Increment(ref _calls);

        if (Fails)
        {
            return Task.FromResult<FetchResult>(new FetchEmpty());
        }

        var point = new MarinePoint(
            new DateTimeOffset(2026, 7, 11, 0, 0, 0, TimeSpan.Zero),
            WaveHeight: 0.4,
            WavePeriod: 4.0,
            WaveDirection: 270.0,
            WaterTemperature: 19.0);

        return Task.FromResult<FetchResult>(new FetchSuccess(
            new DomainForecast<MarinePoint>(DomainKind.Marine, now, TimeResolution.Hourly, [point])));
    }
}
