using Njord.Messages.Alerts;

namespace Njord.Alerts.Tests;

public sealed class ConfidenceThresholdsSpec
{
    [Theory]
    [InlineData(0.0, ConfidenceLevel.High)]
    [InlineData(0.99, ConfidenceLevel.High)]
    [InlineData(1.0, ConfidenceLevel.Medium)]
    [InlineData(2.0, ConfidenceLevel.Medium)]
    [InlineData(3.0, ConfidenceLevel.Medium)]
    [InlineData(3.01, ConfidenceLevel.Low)]
    [InlineData(10.0, ConfidenceLevel.Low)]
    public void Default_thresholds_map_the_spread_to_a_level(double stdDev, ConfidenceLevel expected)
    {
        Assert.Equal(expected, ConfidenceThresholds.Default.LevelFor(stdDev));
    }

    [Fact]
    public void Missing_spread_is_medium()
    {
        Assert.Equal(ConfidenceLevel.Medium, ConfidenceThresholds.Default.LevelFor(null));
    }

    [Fact]
    public void Custom_thresholds_apply()
    {
        var thresholds = new ConfidenceThresholds(0.5, 1.5);

        Assert.Multiple(
            () => Assert.Equal(ConfidenceLevel.High, thresholds.LevelFor(0.4)),
            () => Assert.Equal(ConfidenceLevel.Medium, thresholds.LevelFor(1.0)),
            () => Assert.Equal(ConfidenceLevel.Low, thresholds.LevelFor(2.0)));
    }

    [Theory]
    [InlineData(ConfidenceLevel.High, 1.0, "high")]
    [InlineData(ConfidenceLevel.Medium, 0.5, "medium")]
    [InlineData(ConfidenceLevel.Low, 0.25, "low")]
    public void Levels_map_to_confidence_and_wire_name(ConfidenceLevel level, double confidence, string wire)
    {
        Assert.Multiple(
            () => Assert.Equal(confidence, level.ToConfidence()),
            () => Assert.Equal(wire, level.ToWireName()));
    }
}
