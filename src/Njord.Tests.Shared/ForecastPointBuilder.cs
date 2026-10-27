using Njord.Forecast;
using Njord.Forecast.Forecast;

namespace Njord.Tests.Shared;

public sealed class ForecastPointBuilder
{
    private static readonly DateTimeOffset DefaultStart = new(2026, 7, 14, 12, 0, 0, TimeSpan.Zero);

    private DateTimeOffset _start = DefaultStart;
    private int _hours = 48;
    private Func<int, ForecastPoint, ForecastPoint> _shape = static (_, point) => point;

    private ForecastPointBuilder(Location location) => Location = location;

    public Location Location { get; }

    public static ForecastPointBuilder ForLocation(string name) => new(new Location(name, 0, 0));

    public static ForecastPointBuilder ForLocation(Location location) => new(location);

    public ForecastPointBuilder Starting(DateTimeOffset start)
    {
        _start = start;
        return this;
    }

    public ForecastPointBuilder WithHours(int hours)
    {
        _hours = hours;
        return this;
    }

    public ForecastPointBuilder WithTemperature(double temperature) =>
        Shape((_, point) => point with { Temperature = temperature, ApparentTemperature = temperature });

    public ForecastPointBuilder WithHour(int hour, Func<ForecastPoint, ForecastPoint> change) =>
        Shape((h, point) => h == hour ? change(point) : point);

    public ForecastPointBuilder Shape(Func<int, ForecastPoint, ForecastPoint> change)
    {
        var previous = _shape;
        _shape = (hour, point) => change(hour, previous(hour, point));
        return this;
    }

    public DomainForecast<ForecastPoint> Build() =>
        DomainData.Forecast(_start, _hours, _shape);
}
