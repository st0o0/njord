namespace Njord.Forecast;

public sealed class ForecastSeries
{
    public IReadOnlyList<ForecastPoint> Points { get; }

    public ForecastSeries(IEnumerable<ForecastPoint> points)
    {
        Points = [.. points.OrderBy(p => p.ValidAt)];
    }
}
