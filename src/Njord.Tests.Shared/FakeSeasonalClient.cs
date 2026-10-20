using System.Collections.Immutable;
using Njord.Core.Domain;
using Njord.Forecast;
using Njord.Forecast.Seasonal;

namespace Njord.Tests.Shared;

public sealed class FakeSeasonalClient : IDomainClient
{
    private static readonly double[] Temperature = [1.0, 1.5, 0.5, 2.0, 1.0];
    private static readonly double[] Precipitation = [5.0, -10.0, 0.0, 4.0, 8.0];
    private static readonly double[] Mean = [80.0, 90.0, 100.0, 100.0, 90.0];

    public DomainKind Domain => DomainKind.Seasonal;

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

        var points = Enumerable.Range(0, Temperature.Length)
            .Select(i => new SeasonalPoint(
                new DateTimeOffset(2026, 7 + i, 1, 0, 0, 0, TimeSpan.Zero),
                TemperatureAnomaly: Temperature[i],
                PrecipitationAnomaly: Precipitation[i],
                PrecipitationMean: Mean[i]))
            .ToImmutableArray();

        return Task.FromResult<FetchResult>(new FetchSuccess(
            new DomainForecast<SeasonalPoint>(DomainKind.Seasonal, now, TimeResolution.Monthly, points)));
    }
}
