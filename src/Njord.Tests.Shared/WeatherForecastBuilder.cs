using Njord.Model;

namespace Njord.Tests.Shared;

public sealed class WeatherForecastBuilder
{
    private static readonly DateTimeOffset DefaultStart = new(2026, 7, 14, 12, 0, 0, TimeSpan.Zero);

    private DateTimeOffset _start = DefaultStart;
    private int _hours = 48;
    private Func<int, WeatherPoint, WeatherPoint> _shape = static (_, point) => point;

    private WeatherForecastBuilder(Location location) => Location = location;

    public Location Location { get; }

    public static WeatherForecastBuilder ForLocation(string name) => new(new Location(name, 0, 0));

    public static WeatherForecastBuilder ForLocation(Location location) => new(location);

    public WeatherForecastBuilder Starting(DateTimeOffset start)
    {
        _start = start;
        return this;
    }

    public WeatherForecastBuilder WithHours(int hours)
    {
        _hours = hours;
        return this;
    }

    public WeatherForecastBuilder WithTemperature(double temperature) =>
        Shape((_, point) => point with { Temperature = temperature, ApparentTemperature = temperature });

    public WeatherForecastBuilder WithHour(int hour, Func<WeatherPoint, WeatherPoint> change) =>
        Shape((h, point) => h == hour ? change(point) : point);

    public WeatherForecastBuilder Shape(Func<int, WeatherPoint, WeatherPoint> change)
    {
        var previous = _shape;
        _shape = (hour, point) => change(hour, previous(hour, point));
        return this;
    }

    public DomainForecast<WeatherPoint> Build() =>
        DomainData.Weather(_start, _hours, _shape);
}
