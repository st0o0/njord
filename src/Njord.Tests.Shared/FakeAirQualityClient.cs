using Njord.Core.Domain;
using Njord.Forecast;
using Njord.Forecast.AirQuality;

namespace Njord.Tests.Shared;

public sealed class FakeAirQualityClient : IDomainClient
{
    public DomainKind Domain => DomainKind.AirQuality;

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

        var point = new AirQualityPoint(
            new DateTimeOffset(2026, 7, 11, 3, 0, 0, TimeSpan.Zero),
            Pm25: 8.0,
            Pm10: 15.0,
            Ozone: 60.0);

        return Task.FromResult<FetchResult>(new FetchSuccess(
            new DomainForecast<AirQualityPoint>(DomainKind.AirQuality, now, TimeResolution.Hourly, [point])));
    }
}
