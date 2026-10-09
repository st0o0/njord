using Njord.Compute.Analysis;
using Njord.Weather;

namespace Njord.Compute.Tests.Analysis;

public sealed class NearestPointFinderSpec
{
    private static readonly DateTimeOffset Target = new(2026, 7, 11, 12, 20, 0, TimeSpan.Zero);

    private static ForecastPoint PointAt(int hour) =>
        new(new DateTimeOffset(2026, 7, 11, hour, 0, 0, TimeSpan.Zero), new Dictionary<ParameterDef, double?>());

    [Fact]
    public void Find_returns_point_closest_to_target()
    {
        var points = new[] { PointAt(10), PointAt(12), PointAt(14) };

        var result = NearestPointFinder.Find(points, Target);

        Assert.NotNull(result);
        Assert.Equal(PointAt(12).ValidAt, result.ValidAt);
    }

    [Fact]
    public void Find_considers_points_before_the_target()
    {
        var points = new[] { PointAt(11), PointAt(15) };

        var result = NearestPointFinder.Find(points, Target);

        Assert.NotNull(result);
        Assert.Equal(PointAt(11).ValidAt, result.ValidAt);
    }

    [Fact]
    public void Find_returns_null_for_empty_series()
    {
        var result = NearestPointFinder.Find([], Target);

        Assert.Null(result);
    }
}
