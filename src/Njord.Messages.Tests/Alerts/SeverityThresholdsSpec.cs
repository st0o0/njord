using Njord.Messages.Alerts;

namespace Njord.Messages.Tests.Alerts;

public sealed class SeverityThresholdsSpec
{
    private static readonly SeverityThresholds Cold = new(0, -5, -10, ScanDirection.Min);
    private static readonly SeverityThresholds Hot = new(30, 35, 40, ScanDirection.Max);

    [Theory]
    [InlineData(5, AlertSeverity.None, 0)]
    [InlineData(-2, AlertSeverity.Yellow, 0)]
    [InlineData(-7, AlertSeverity.Orange, -5)]
    [InlineData(-12, AlertSeverity.Red, -10)]
    public void Should_classify_minimum_scans_by_the_lowest_crossed_level(
        double value, AlertSeverity severity, double threshold)
    {
        Assert.Equal((severity, threshold), Cold.Classify(value));
    }

    [Theory]
    [InlineData(25, AlertSeverity.None)]
    [InlineData(31, AlertSeverity.Yellow)]
    [InlineData(36, AlertSeverity.Orange)]
    [InlineData(41, AlertSeverity.Red)]
    public void Should_classify_maximum_scans_by_the_highest_crossed_level(double value, AlertSeverity severity)
    {
        Assert.Equal(severity, Hot.Classify(value).Severity);
    }

    [Fact]
    public void Should_include_the_boundary_unless_strict()
    {
        Assert.Multiple(
            () => Assert.Equal(AlertSeverity.Yellow, Hot.Classify(30).Severity),
            () => Assert.Equal(AlertSeverity.None, Hot.Classify(30, strict: true).Severity),
            () => Assert.True(Cold.CrossesAdvisory(0)),
            () => Assert.False(Cold.CrossesAdvisory(0, strict: true)));
    }

    [Fact]
    public void Should_create_thresholds_from_the_first_three_values()
    {
        var created = SeverityThresholds.TryCreate([1, 2, 3, 4], ScanDirection.Max, out var thresholds);

        Assert.True(created);
        Assert.Equal(new SeverityThresholds(1, 2, 3, ScanDirection.Max), thresholds);
    }

    [Fact]
    public void Should_refuse_to_create_thresholds_from_fewer_than_three_values()
    {
        var created = SeverityThresholds.TryCreate([1, 2], ScanDirection.Min, out var thresholds);

        Assert.False(created);
        Assert.Null(thresholds);
    }
}
