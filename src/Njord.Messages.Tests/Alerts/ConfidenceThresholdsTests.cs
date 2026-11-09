using Njord.Messages.Alerts;

namespace Njord.Messages.Tests.Alerts;

public sealed class ConfidenceThresholdsTests
{
    private static readonly ConfidenceThresholds _thresholds = ConfidenceThresholds.Default;

    [Theory]
    [InlineData(null, ConfidenceLevel.Medium)]
    [InlineData(0.5, ConfidenceLevel.High)]
    [InlineData(4.0, ConfidenceLevel.Low)]
    [InlineData(2.0, ConfidenceLevel.Medium)]
    public void LevelFor_classifies_standard_deviation(double? stdDev, ConfidenceLevel expected) => Assert.Equal(expected, _thresholds.LevelFor(stdDev));

    [Fact]
    public void LevelFor_returns_high_at_the_boundary()
    {
        var thresholds = new ConfidenceThresholds(1.0, 3.0);
        Assert.Equal(ConfidenceLevel.Medium, thresholds.LevelFor(1.0));
    }

    [Fact]
    public void LevelFor_returns_low_at_the_boundary()
    {
        var thresholds = new ConfidenceThresholds(1.0, 3.0);
        Assert.Equal(ConfidenceLevel.Medium, thresholds.LevelFor(3.0));
    }
}
