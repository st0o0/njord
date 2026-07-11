namespace Njord.Domain;

/// <summary>An ordered (ascending ValidAt) series of forecast points.</summary>
public sealed class ForecastSeries
{
    public IReadOnlyList<ForecastPoint> Points { get; }

    public ForecastSeries(IEnumerable<ForecastPoint> points)
    {
        Points = [.. points.OrderBy(p => p.ValidAt)];
    }
}
