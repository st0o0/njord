using Njord.Messages.Alerts;

namespace Njord.Messages.Tests.Alerts;

public sealed class AlertWireNamesSpec
{
    [Theory]
    [InlineData(AlertSeverity.None, "none")]
    [InlineData(AlertSeverity.Yellow, "yellow")]
    [InlineData(AlertSeverity.Orange, "orange")]
    [InlineData(AlertSeverity.Red, "red")]
    public void Should_map_severity_to_wire_name(AlertSeverity severity, string expected)
    {
        Assert.Equal(expected, severity.ToWireName());
    }

    [Theory]
    [InlineData(ConfidenceLevel.Low, "low", 0.25)]
    [InlineData(ConfidenceLevel.Medium, "medium", 0.5)]
    [InlineData(ConfidenceLevel.High, "high", 1.0)]
    public void Should_map_confidence_level_to_wire_name_and_score(ConfidenceLevel level, string name, double score)
    {
        Assert.Multiple(
            () => Assert.Equal(name, level.ToWireName()),
            () => Assert.Equal(score, level.ToConfidence()));
    }

    [Fact]
    public void Should_map_detail_enums_to_wire_names()
    {
        Assert.Multiple(
            () => Assert.Equal("very_high", UvLevel.VeryHigh.ToWireName()),
            () => Assert.Equal("frostbite_10min", ExposureRisk.Frostbite10Min.ToWireName()),
            () => Assert.Equal("oppressive", HumidityComfort.Oppressive.ToWireName()));
    }

    [Fact]
    public void Should_throw_for_undefined_severity()
    {
        var ex = Assert.Throws<ArgumentOutOfRangeException>(() => ((AlertSeverity)42).ToWireName());

        Assert.Equal("severity", ex.ParamName);
    }
}
