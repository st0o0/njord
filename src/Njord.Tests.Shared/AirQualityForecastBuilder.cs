using Njord.Model;

namespace Njord.Tests.Shared;

public sealed class AirQualityForecastBuilder
{
    private static readonly DateTimeOffset DefaultStart = new(2026, 7, 14, 12, 0, 0, TimeSpan.Zero);

    private DateTimeOffset _start = DefaultStart;
    private int _hours = 24;
    private Func<int, AirQualityPoint, AirQualityPoint> _shape = static (_, point) => point;

    private AirQualityForecastBuilder()
    {
    }

    public static AirQualityForecastBuilder Default() => new();

    public AirQualityForecastBuilder Starting(DateTimeOffset start)
    {
        _start = start;
        return this;
    }

    public AirQualityForecastBuilder WithHours(int hours)
    {
        _hours = hours;
        return this;
    }

    public AirQualityForecastBuilder WithPm25(double pm25) => Shape((_, point) => point with { Pm25 = pm25 });

    public AirQualityForecastBuilder WithOzone(double ozone) => Shape((_, point) => point with { Ozone = ozone });

    public AirQualityForecastBuilder Shape(Func<int, AirQualityPoint, AirQualityPoint> change)
    {
        var previous = _shape;
        _shape = (hour, point) => change(hour, previous(hour, point));
        return this;
    }

    public DomainForecast<AirQualityPoint> Build()
    {
        var points = Enumerable.Range(0, _hours).Select(hour =>
            _shape(hour, new AirQualityPoint(_start.AddHours(hour), Pm25: 5.0, Pm10: 10.0, Ozone: 40.0)));
        return new DomainForecast<AirQualityPoint>(DomainKind.AirQuality, _start, TimeResolution.Hourly, [.. points]);
    }
}
