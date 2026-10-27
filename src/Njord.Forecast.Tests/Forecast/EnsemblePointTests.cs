using Njord.Forecast.Forecast;

namespace Njord.Forecast.Tests.Forecast;

public sealed class EnsemblePointTests
{
    private static readonly DateTimeOffset T0 = new(2026, 7, 1, 6, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset T1 = T0.AddHours(1);
    private static readonly DateTimeOffset T2 = T0.AddHours(2);

    [Fact]
    public void SpreadAt_returns_exact_match()
    {
        var points = new List<EnsemblePoint>
        {
            new(T0, TemperatureSpread: 1.0),
            new(T1, TemperatureSpread: 2.0),
            new(T2, TemperatureSpread: 3.0),
        };

        var result = points.SpreadAt(T1);

        Assert.NotNull(result);
        Assert.Equal(2.0, result.TemperatureSpread);
    }

    [Fact]
    public void SpreadAt_returns_closest_when_no_exact_match()
    {
        var points = new List<EnsemblePoint>
        {
            new(T0, TemperatureSpread: 1.0),
            new(T2, TemperatureSpread: 3.0),
        };

        var result = points.SpreadAt(T1);

        Assert.NotNull(result);
        Assert.Equal(1.0, result.TemperatureSpread);
    }

    [Fact]
    public void SpreadAt_returns_null_for_empty_list()
    {
        var points = new List<EnsemblePoint>();

        Assert.Null(points.SpreadAt(T0));
    }

    [Fact]
    public void Implements_IHasValidTime()
    {
        IHasValidTime point = new EnsemblePoint(T0);

        Assert.Equal(T0, point.ValidFrom);
    }

    [Fact]
    public void Spread_fields_default_to_null()
    {
        var point = new EnsemblePoint(T0);

        Assert.Multiple(
            () => Assert.Null(point.TemperatureSpread),
            () => Assert.Null(point.PrecipitationSpread),
            () => Assert.Null(point.WindSpeedSpread));
    }
}
