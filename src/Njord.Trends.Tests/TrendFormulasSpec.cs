using Njord.Messages.Trends;

namespace Njord.Trends.Tests;

public sealed class TrendFormulasSpec
{

    // --- TrendDirection ---

    [Fact]
    public void TrendDirection_rising()
    {
        var result = TrendFormulas.Direction(18.0, 22.0, 0.5);
        Assert.NotNull(result);
        Assert.Equal(TrendDirection.Rising, result.Value.Direction);
        Assert.Equal(4.0, result.Value.Delta);
    }

    [Fact]
    public void TrendDirection_falling()
    {
        var result = TrendFormulas.Direction(22.0, 18.0, 0.5);
        Assert.NotNull(result);
        Assert.Equal(TrendDirection.Falling, result.Value.Direction);
        Assert.Equal(-4.0, result.Value.Delta);
    }

    [Fact]
    public void TrendDirection_stable_within_deadband()
    {
        var result = TrendFormulas.Direction(20.0, 20.3, 0.5);
        Assert.NotNull(result);
        Assert.Equal(TrendDirection.Stable, result.Value.Direction);
        Assert.Equal(0.3, result.Value.Delta);
    }

    [Fact]
    public void TrendDirection_null_previous() =>
        Assert.Null(TrendFormulas.Direction(null, 20.0, 0.5));

    [Fact]
    public void TrendDirection_null_current() =>
        Assert.Null(TrendFormulas.Direction(20.0, null, 0.5));

    // --- WeatherChange ---

    [Fact]
    public void WeatherChange_clear_to_rain()
    {
        var result = TrendFormulas.WeatherChange(1, 63);
        Assert.NotNull(result);
        Assert.Equal(WeatherCategory.Clear, result.From);
        Assert.Equal(WeatherCategory.Rain, result.To);
        Assert.Equal("clear → rain", result.ToDescription());
    }

    [Fact]
    public void WeatherChange_same_category_returns_null() =>
        Assert.Null(TrendFormulas.WeatherChange(61, 65));

    [Fact]
    public void WeatherChange_null_codes() =>
        Assert.Null(TrendFormulas.WeatherChange(null, 63));

    // --- ConsensusStability ---

    [Fact]
    public void ConsensusStability_converging()
    {
        var result = TrendFormulas.ConsensusStability(5.0, 3.0);
        Assert.NotNull(result);
        Assert.Equal(Stability.Converging, result.Value.Stability);
        Assert.Equal(0.6, result.Value.Ratio);
    }

    [Fact]
    public void ConsensusStability_diverging()
    {
        var result = TrendFormulas.ConsensusStability(3.0, 5.0);
        Assert.NotNull(result);
        Assert.Equal(Stability.Diverging, result.Value.Stability);
        Assert.InRange(result.Value.Ratio, 1.66, 1.68);
    }

    [Fact]
    public void ConsensusStability_stable()
    {
        var result = TrendFormulas.ConsensusStability(4.0, 4.2);
        Assert.NotNull(result);
        Assert.Equal(Stability.Stable, result.Value.Stability);
        Assert.Equal(1.05, result.Value.Ratio);
    }

    [Fact]
    public void ConsensusStability_null_iqr() =>
        Assert.Null(TrendFormulas.ConsensusStability(null, 3.0));

    [Fact]
    public void ConsensusStability_zero_previous() =>
        Assert.Null(TrendFormulas.ConsensusStability(0.0, 3.0));

    // --- PredictabilityDecay ---

    [Fact]
    public void PredictabilityDecay_gradual()
    {
        var spreads = new (int, double?)[]
            { (3, 1.0), (6, 1.5), (12, 2.5), (24, 4.0), (48, 7.0), (72, 10.0) };

        var result = TrendFormulas.PredictabilityDecay(spreads);
        Assert.NotNull(result);
        Assert.True(result.Value.DecayRate > 0);
        Assert.Equal(24, result.Value.ReliableHours);
    }

    [Fact]
    public void PredictabilityDecay_flat()
    {
        var spreads = new (int, double?)[]
            { (3, 2.0), (6, 2.1), (12, 2.0), (24, 2.2) };

        var result = TrendFormulas.PredictabilityDecay(spreads);
        Assert.NotNull(result);
        Assert.InRange(result.Value.DecayRate, -0.01, 0.02);
        Assert.Null(result.Value.ReliableHours);
    }

    [Fact]
    public void PredictabilityDecay_insufficient_data() =>
        Assert.Null(TrendFormulas.PredictabilityDecay([(3, 1.0)]));
}
