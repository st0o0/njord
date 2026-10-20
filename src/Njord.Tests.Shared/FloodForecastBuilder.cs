using Njord.Forecast;
using Njord.Forecast.Flood;

namespace Njord.Tests.Shared;

public sealed class FloodForecastBuilder
{
    private static readonly DateTimeOffset DefaultStart = new(2026, 7, 14, 0, 0, 0, TimeSpan.Zero);

    private DateTimeOffset _start = DefaultStart;
    private int _days = 7;
    private Func<int, FloodPoint, FloodPoint> _shape = static (_, point) => point;

    private FloodForecastBuilder()
    {
    }

    public static FloodForecastBuilder Default() => new();

    public FloodForecastBuilder Starting(DateTimeOffset start)
    {
        _start = start;
        return this;
    }

    public FloodForecastBuilder WithDays(int days)
    {
        _days = days;
        return this;
    }

    public FloodForecastBuilder WithDischarge(double discharge) =>
        Shape((_, point) => point with { Discharge = discharge });

    public FloodForecastBuilder Shape(Func<int, FloodPoint, FloodPoint> change)
    {
        var previous = _shape;
        _shape = (day, point) => change(day, previous(day, point));
        return this;
    }

    public DomainForecast<FloodPoint> Build()
    {
        var points = Enumerable.Range(0, _days).Select(day =>
            _shape(day, new FloodPoint(_start.AddDays(day), Discharge: 12.5)));
        return new DomainForecast<FloodPoint>(DomainKind.Flood, _start, TimeResolution.Daily, [.. points]);
    }
}
