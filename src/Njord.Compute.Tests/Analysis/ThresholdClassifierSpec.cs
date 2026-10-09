using Njord.Compute.Analysis;

namespace Njord.Compute.Tests.Analysis;

public sealed class ThresholdClassifierSpec
{
    private static readonly double[] Ascending = [10.0, 20.0, 30.0];
    private static readonly double[] Descending = [0.0, -5.0, -10.0];

    [Theory]
    [InlineData(35.0, AlertSeverity.Red, 30.0)]
    [InlineData(30.0, AlertSeverity.Red, 30.0)]
    [InlineData(25.0, AlertSeverity.Orange, 20.0)]
    [InlineData(10.0, AlertSeverity.Yellow, 10.0)]
    [InlineData(9.9, AlertSeverity.None, 10.0)]
    public void Max_direction_classifies_inclusive_tiers(double value, AlertSeverity severity, double threshold)
    {
        var result = ThresholdClassifier.Classify(value, Ascending, ScanDirection.Max);

        Assert.Multiple(
            () => Assert.Equal(severity, result.Severity),
            () => Assert.Equal(threshold, result.EffectiveThreshold));
    }

    [Theory]
    [InlineData(-12.0, AlertSeverity.Red, -10.0)]
    [InlineData(-10.0, AlertSeverity.Red, -10.0)]
    [InlineData(-6.0, AlertSeverity.Orange, -5.0)]
    [InlineData(0.0, AlertSeverity.Yellow, 0.0)]
    [InlineData(0.1, AlertSeverity.None, 0.0)]
    public void Min_direction_classifies_inclusive_tiers(double value, AlertSeverity severity, double threshold)
    {
        var result = ThresholdClassifier.Classify(value, Descending, ScanDirection.Min);

        Assert.Multiple(
            () => Assert.Equal(severity, result.Severity),
            () => Assert.Equal(threshold, result.EffectiveThreshold));
    }

    [Fact]
    public void Strict_max_does_not_trigger_on_exact_threshold()
    {
        var result = ThresholdClassifier.Classify(10.0, Ascending, ScanDirection.Max, strict: true);

        Assert.Equal(AlertSeverity.None, result.Severity);
        Assert.Equal(10.0, result.EffectiveThreshold);
    }

    [Fact]
    public void Strict_min_does_not_trigger_on_exact_threshold()
    {
        var result = ThresholdClassifier.Classify(0.0, Descending, ScanDirection.Min, strict: true);

        Assert.Equal(AlertSeverity.None, result.Severity);
        Assert.Equal(0.0, result.EffectiveThreshold);
    }
}
