namespace Njord.Domain.Weather;

/// <summary>An ordered (ascending ValidAt) series of forecast points.</summary>
public sealed class ForecastSeries
{
    public IReadOnlyList<ForecastPoint> Points { get; }

    public ForecastSeries(IEnumerable<ForecastPoint> points)
    {
        Points = [.. points.OrderBy(p => p.ValidAt)];
    }

    public ForecastSeries Window(DateTimeOffset from, DateTimeOffset to)
        => new(Points.Where(p => p.ValidAt >= from && p.ValidAt <= to));

    public double? Mean(ParameterDef param, DateTimeOffset from, DateTimeOffset to)
    {
        double sum = 0;
        var count = 0;
        foreach (var point in Points)
        {
            if (point.ValidAt < from || point.ValidAt > to) continue;
            if (point.Get(param) is not { } v) continue;
            sum += v;
            count++;
        }
        return count > 0 ? sum / count : null;
    }

    public IEnumerable<double> Values(ParameterDef param, DateTimeOffset from, DateTimeOffset to)
    {
        foreach (var point in Points)
        {
            if (point.ValidAt < from || point.ValidAt > to) continue;
            if (point.Get(param) is { } v) yield return v;
        }
    }
}
