using Njord.Core;
using Njord.Core.Domain;
using Njord.Core.Flood;
using Njord.Core.Forecast;
using Njord.Messages.Fetch;

namespace Njord.Tests.Shared;

public sealed class FakeFloodClient : IDomainClient
{
    public DomainKind Domain => DomainKind.Flood;

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

        var point = new FloodPoint(
            new DateTimeOffset(2026, 7, 11, 0, 0, 0, TimeSpan.Zero),
            Discharge: 120.0,
            DischargeMedian: 100.0,
            DischargeP75: 140.0);

        return Task.FromResult<FetchResult>(new FetchSuccess(
            new DomainForecast<FloodPoint>(DomainKind.Flood, now, TimeResolution.Daily, [point])));
    }
}
