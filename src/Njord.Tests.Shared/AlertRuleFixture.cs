using System.Collections.Immutable;
using Njord.Core;
using Njord.Core.Forecast;

namespace Njord.Tests.Shared;

public static class AlertRuleFixture
{
    public static readonly DateTimeOffset Now = new(2026, 7, 11, 12, 0, 0, TimeSpan.Zero);

    public static ForecastPoint Benign(DateTimeOffset validFrom) => new(
        ValidFrom: validFrom,
        Temperature: 15.0,
        ApparentTemperature: 15.0,
        WindSpeed: 5.0,
        Precipitation: 0.0,
        PrecipitationProbability: 0.0,
        CloudCover: 50.0,
        Pressure: 1013.0,
        WeatherCode: 0,
        Humidity: 50.0,
        DewPoint: 5.0,
        UvIndex: 0.0);

    public static DomainForecast<ForecastPoint> Hourly(int hours, Func<int, ForecastPoint, ForecastPoint>? shape = null)
    {
        var points = Enumerable.Range(0, hours)
            .Select(hour =>
            {
                var point = Benign(Now.AddHours(hour));
                return shape is null ? point : shape(hour, point);
            })
            .ToImmutableArray();
        return new DomainForecast<ForecastPoint>(DomainKind.Forecast, Now, TimeResolution.Hourly, points);
    }

    public static DomainForecast<ForecastPoint> Day(Func<int, ForecastPoint, ForecastPoint>? shape = null) => Hourly(24, shape);

    public static DomainForecast<ForecastPoint> Uniform(Func<ForecastPoint, ForecastPoint> change) => Day((_, point) => change(point));

    public static DomainForecast<ForecastPoint> Empty() => Hourly(0);

    public static DomainForecast<EnsemblePoint> EnsembleHourly(int hours, double spread = 1.5)
    {
        var points = Enumerable.Range(0, hours)
            .Select(hour => new EnsemblePoint(Now.AddHours(hour), spread, spread, spread))
            .ToImmutableArray();
        return new DomainForecast<EnsemblePoint>(DomainKind.Ensemble, Now, TimeResolution.Hourly, points);
    }

    public static DomainForecast<EnsemblePoint> EnsembleDay(double spread = 1.5) => EnsembleHourly(24, spread);
}
