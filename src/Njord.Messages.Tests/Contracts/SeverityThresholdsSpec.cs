using Njord.Consensus;
using Njord.Consensus.Querying;
using Njord.Messages.Alerts;

namespace Njord.Messages.Tests.Contracts;

public sealed class SeverityThresholdsSpec
{
    private static readonly SeverityThresholds Ascending = new(10.0, 20.0, 30.0, ScanDirection.Max);
    private static readonly SeverityThresholds Descending = new(0.0, -5.0, -10.0, ScanDirection.Min);

    [Theory]
    [InlineData(35.0, AlertSeverity.Red, 30.0)]
    [InlineData(30.0, AlertSeverity.Red, 30.0)]
    [InlineData(25.0, AlertSeverity.Orange, 20.0)]
    [InlineData(10.0, AlertSeverity.Yellow, 10.0)]
    [InlineData(9.9, AlertSeverity.None, 10.0)]
    public void Max_direction_classifies_inclusive_tiers(double value, AlertSeverity severity, double threshold)
    {
        var result = Ascending.Classify(value);

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
        var result = Descending.Classify(value);

        Assert.Multiple(
            () => Assert.Equal(severity, result.Severity),
            () => Assert.Equal(threshold, result.EffectiveThreshold));
    }

    [Fact]
    public void Strict_max_does_not_trigger_on_exact_threshold()
    {
        var result = Ascending.Classify(10.0, strict: true);

        Assert.Equal(AlertSeverity.None, result.Severity);
        Assert.Equal(10.0, result.EffectiveThreshold);
    }

    [Fact]
    public void Strict_min_does_not_trigger_on_exact_threshold()
    {
        var result = Descending.Classify(0.0, strict: true);

        Assert.Equal(AlertSeverity.None, result.Severity);
        Assert.Equal(0.0, result.EffectiveThreshold);
    }

    [Fact]
    public void Advisory_crossing_follows_the_direction()
    {
        Assert.Multiple(
            () => Assert.True(Ascending.CrossesAdvisory(10.0)),
            () => Assert.False(Ascending.CrossesAdvisory(9.0)),
            () => Assert.True(Descending.CrossesAdvisory(-1.0)),
            () => Assert.False(Descending.CrossesAdvisory(1.0)),
            () => Assert.False(Descending.CrossesAdvisory(0.0, strict: true)));
    }

    [Fact]
    public void Three_configured_values_convert_to_thresholds()
    {
        Assert.True(SeverityThresholds.TryCreate([0, -5, -15], ScanDirection.Min, out var thresholds));
        Assert.Equal(new SeverityThresholds(0, -5, -15, ScanDirection.Min), thresholds);
    }

    [Theory]
    [InlineData(new double[0])]
    [InlineData(new[] { 1.0, 2.0 })]
    public void Fewer_than_three_configured_values_do_not_convert(double[] values)
    {
        Assert.False(SeverityThresholds.TryCreate(values, ScanDirection.Max, out _));
    }
}
