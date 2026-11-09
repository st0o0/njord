using Njord.Core.Forecast;

namespace Njord.Core.Tests.Forecast;

public sealed class EnsemblePointTests
{
    private static readonly DateTimeOffset _t0 = new(2026, 7, 1, 6, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset _t1 = _t0.AddHours(1);
    private static readonly DateTimeOffset _t2 = _t0.AddHours(2);

    [Fact]
    public void SpreadAt_returns_exact_match()
    {
        var points = new List<EnsemblePoint>
        {
            new(_t0, TemperatureSpread: 1.0),
            new(_t1, TemperatureSpread: 2.0),
            new(_t2, TemperatureSpread: 3.0),
        };

        var result = points.SpreadAt(_t1);

        Assert.NotNull(result);
        Assert.Equal(2.0, result.TemperatureSpread);
    }

    [Fact]
    public void SpreadAt_returns_closest_when_no_exact_match()
    {
        var points = new List<EnsemblePoint>
        {
            new(_t0, TemperatureSpread: 1.0),
            new(_t2, TemperatureSpread: 3.0),
        };

        var result = points.SpreadAt(_t1);

        Assert.NotNull(result);
        Assert.Equal(1.0, result.TemperatureSpread);
    }

    [Fact]
    public void SpreadAt_returns_null_for_empty_list()
    {
        var points = new List<EnsemblePoint>();

        Assert.Null(points.SpreadAt(_t0));
    }

    [Fact]
    public void Implements_IHasValidTime()
    {
        IHasValidTime point = new EnsemblePoint(_t0);

        Assert.Equal(_t0, point.ValidFrom);
    }

    [Fact]
    public void Spread_fields_default_to_null()
    {
        var point = new EnsemblePoint(_t0);

        Assert.Multiple(
            () => Assert.Null(point.TemperatureSpread),
            () => Assert.Null(point.PrecipitationSpread),
            () => Assert.Null(point.WindSpeedSpread));
    }
}
