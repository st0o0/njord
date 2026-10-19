using Njord.Messages.Alerts;

namespace Njord.Messages.Tests.Contracts;

public sealed class CategoricalValueSpec
{
    [Theory]
    [InlineData(AlertSeverity.None, "none")]
    [InlineData(AlertSeverity.Yellow, "yellow")]
    [InlineData(AlertSeverity.Orange, "orange")]
    [InlineData(AlertSeverity.Red, "red")]
    public void Alert_severity_wire_name_is_lowercase(AlertSeverity severity, string expected) =>
        Assert.Equal(expected, severity.ToWireName());

    [Fact]
    public void Undefined_alert_severity_has_no_wire_name()
    {
        var ex = Assert.Throws<ArgumentOutOfRangeException>(() => ((AlertSeverity)99).ToWireName());

        Assert.Equal("severity", ex.ParamName);
    }
}
