using Njord.Weather;

namespace Njord.Compute.Analysis;

public static class NearestPointFinder
{
    public static ForecastPoint? Find(IEnumerable<ForecastPoint> points, DateTimeOffset target) =>
        points
            .OrderBy(p => Math.Abs((p.ValidAt - target).TotalMinutes))
            .FirstOrDefault();
}
