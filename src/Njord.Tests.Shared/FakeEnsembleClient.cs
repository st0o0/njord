using System.Collections.Immutable;
using Njord.Core.Domain;
using Njord.Forecast;
using Njord.Forecast.Forecast;

namespace Njord.Tests.Shared;

public sealed class FakeEnsembleClient : IDomainClient
{
    public DomainKind Domain => DomainKind.Ensemble;

    public int CallWeight { get; set; } = 1;

    public bool Fails { get; set; }

    public ImmutableArray<string> ModelIds { get; set; } = [];

    private int _calls;

    public int Calls => Volatile.Read(ref _calls);

    public Task<FetchResult> FetchAsync(double latitude, double longitude, DateTimeOffset now, CancellationToken cancellationToken)
    {
        Interlocked.Increment(ref _calls);

        if (Fails)
        {
            return Task.FromResult<FetchResult>(new FetchEmpty());
        }

        var point = new EnsemblePoint(
            new DateTimeOffset(2026, 7, 11, 3, 0, 0, TimeSpan.Zero),
            TemperatureSpread: 1.5,
            PrecipitationSpread: 0.3,
            WindSpeedSpread: 2.0);

        var forecast = new DomainForecast<EnsemblePoint>(DomainKind.Ensemble, now, TimeResolution.Hourly, [point]);

        var models = ModelIds.IsDefaultOrEmpty
            ? ImmutableDictionary<string, IDomainForecast>.Empty
            : ModelIds.ToImmutableDictionary(
                id => id,
                id => (IDomainForecast)new DomainForecast<ForecastPoint>(DomainKind.Forecast, now, TimeResolution.Hourly,
                [
                    new ForecastPoint(
                        new DateTimeOffset(2026, 7, 11, 3, 0, 0, TimeSpan.Zero),
                        Temperature: 10.0 + id.Length,
                        Precipitation: 0.0)
                ]));

        return Task.FromResult<FetchResult>(new FetchSuccess(forecast) { Models = models });
    }
}
