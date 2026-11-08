using Njord.Features.Shared;

namespace Njord.Features.Tests.Shared;

public sealed class ScoringTests
{
    [Theory]
    [InlineData(0.0, 0.0, 100.0, 0.0)]
    [InlineData(50.0, 0.0, 100.0, 0.5)]
    [InlineData(100.0, 0.0, 100.0, 1.0)]
    [InlineData(-10.0, 0.0, 100.0, 0.0)]
    [InlineData(200.0, 0.0, 100.0, 1.0)]
    public void Ramp_interpolates_between_zero_and_one(double value, double zeroAt, double oneAt, double expected)
    {
        Assert.Equal(expected, Scoring.Ramp(value, zeroAt, oneAt));
    }

    [Fact]
    public void Ramp_handles_equal_bounds()
    {
        Assert.Equal(1.0, Scoring.Ramp(5.0, 5.0, 5.0));
        Assert.Equal(0.0, Scoring.Ramp(4.9, 5.0, 5.0));
    }

    [Fact]
    public void Ramp_handles_inverted_range()
    {
        Assert.Equal(1.0, Scoring.Ramp(0.0, 100.0, 0.0));
        Assert.Equal(0.5, Scoring.Ramp(50.0, 100.0, 0.0));
        Assert.Equal(0.0, Scoring.Ramp(100.0, 100.0, 0.0));
    }

    [Theory]
    [InlineData(-2.0, 0.0)]
    [InlineData(0.0, 0.0)]
    [InlineData(5.0, 0.5)]
    [InlineData(10.0, 1.0)]
    [InlineData(15.0, 1.0)]
    [InlineData(20.0, 1.0)]
    [InlineData(25.0, 0.5)]
    [InlineData(30.0, 0.0)]
    [InlineData(35.0, 0.0)]
    public void Band_rises_peaks_and_falls(double value, double expected)
    {
        Assert.Equal(expected, Scoring.Band(value, 0.0, 10.0, 20.0, 30.0));
    }

    [Fact]
    public void LongestRun_finds_longest_true_sequence()
    {
        var (start, length) = Scoring.LongestRun([true, true, false, true, true, true, false]);

        Assert.Equal(3, start);
        Assert.Equal(3, length);
    }

    [Fact]
    public void LongestRun_returns_zero_when_all_false()
    {
        var (_, length) = Scoring.LongestRun([false, false, false]);
        Assert.Equal(0, length);
    }

    [Fact]
    public void LongestRun_returns_zero_for_empty_input()
    {
        var (_, length) = Scoring.LongestRun([]);
        Assert.Equal(0, length);
    }

    [Fact]
    public void LongestRun_handles_all_true()
    {
        var (start, length) = Scoring.LongestRun([true, true, true]);

        Assert.Equal(0, start);
        Assert.Equal(3, length);
    }

    [Fact]
    public void BestWindow_returns_window_of_good_hours()
    {
        var (score, start, length) = Scoring.BestWindow([30.0, 80.0, 90.0, 85.0, 20.0], 70.0);

        Assert.Multiple(
            () => Assert.Equal(85, score),
            () => Assert.Equal(1, start),
            () => Assert.Equal(3, length));
    }

    [Fact]
    public void BestWindow_returns_best_single_hour_when_no_good_run()
    {
        var (score, start, length) = Scoring.BestWindow([10.0, 40.0, 30.0], 70.0);

        Assert.Multiple(
            () => Assert.Equal(40, score),
            () => Assert.Equal(1, start),
            () => Assert.Equal(0, length));
    }

    [Fact]
    public void BestWindow_returns_zero_for_empty_input()
    {
        var (score, _, length) = Scoring.BestWindow([], 70.0);

        Assert.Equal(0, score);
        Assert.Equal(0, length);
    }
}
