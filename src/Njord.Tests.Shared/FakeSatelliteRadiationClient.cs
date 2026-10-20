using System.Collections.Immutable;
using Njord.Core.Domain;
using Njord.Forecast;
using Njord.Forecast.Solar;

namespace Njord.Tests.Shared;

public sealed class FakeSatelliteRadiationClient : IDomainClient
{
    private static readonly double[] Ghi = [300.0, 450.0, 520.0];
    private static readonly double[] Direct = [200.0, 300.0, 350.0];
    private static readonly double[] Diffuse = [100.0, 150.0, 170.0];
    private static readonly double[] Dni = [400.0, 500.0, 560.0];
    private static readonly double[] Gti = [350.0, 480.0, 540.0];

    public DomainKind Domain => DomainKind.Satellite;

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

        var points = Enumerable.Range(0, Ghi.Length)
            .Select(i => new SatelliteRadiationPoint(
                new DateTimeOffset(2026, 7, 1, 8 + i, 0, 0, TimeSpan.Zero),
                GHI: Ghi[i],
                DirectRadiation: Direct[i],
                DiffuseRadiation: Diffuse[i],
                DNI: Dni[i],
                GTI: Gti[i]))
            .ToImmutableArray();

        return Task.FromResult<FetchResult>(new FetchSuccess(
            new DomainForecast<SatelliteRadiationPoint>(DomainKind.Satellite, now, TimeResolution.Hourly, points)));
    }
}
