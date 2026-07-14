namespace Njord.Domain.Weather;

public sealed class DailyForecastSeries
{
    public static readonly DailyForecastSeries Empty = new([]);

    public IReadOnlyList<DailyForecastPoint> Points { get; }

    public DailyForecastSeries(IEnumerable<DailyForecastPoint> points)
    {
        Points = [.. points.OrderBy(p => p.Date)];
    }
}
