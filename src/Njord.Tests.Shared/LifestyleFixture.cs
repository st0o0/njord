using System.Collections.Immutable;
using Microsoft.Extensions.Time.Testing;
using Njord.Core.Domain;
using Njord.Forecast;
using Njord.Forecast.AirQuality;
using Njord.Forecast.Forecast;
using static Njord.Tests.Shared.AlertRuleFixture;

namespace Njord.Tests.Shared;

public static class LifestyleFixture
{
    public static readonly Location Lucerne = new("lucerne", 47.05, 8.31);
    public static readonly FakeTimeProvider Time = new(Now);

    public static DomainForecast<AirQualityPoint> CleanAir(int hours = 24, Func<int, AirQualityPoint, AirQualityPoint>? shape = null)
    {
        var points = Enumerable.Range(0, hours)
            .Select(hour =>
            {
                var point = new AirQualityPoint(Now.AddHours(hour), 5.0, 10.0, 40.0);
                return shape is null ? point : shape(hour, point);
            })
            .ToImmutableArray();
        return new DomainForecast<AirQualityPoint>(DomainKind.AirQuality, Now, TimeResolution.Hourly, points);
    }

    public static LocationForecast Combined(DomainForecast<ForecastPoint> weather, DomainForecast<AirQualityPoint> air) =>
        new(Lucerne, weather, air);

    public static LocationForecast ForecastOnly(DomainForecast<ForecastPoint> weather) => new(Lucerne, weather);

    public static LocationForecast ForecastWithEnsemble(DomainForecast<ForecastPoint> weather, DomainForecast<EnsemblePoint> ensemble) =>
        new(Lucerne, weather, Ensemble: ensemble);
}
