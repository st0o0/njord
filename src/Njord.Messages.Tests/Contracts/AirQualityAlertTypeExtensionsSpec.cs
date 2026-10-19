using Njord.Messages.Alerts;

namespace Njord.Messages.Tests.Contracts;

public sealed class AirQualityAlertTypeExtensionsSpec
{
    public static TheoryData<AirQualityAlertType, string> WireNames => new()
    {
        { AirQualityAlertType.Pollen, "pollen" },
        { AirQualityAlertType.Pm25, "pm2_5" },
    };

    [Theory]
    [MemberData(nameof(WireNames))]
    public void ToWireName_returns_the_documented_name(AirQualityAlertType type, string expected)
    {
        Assert.Equal(expected, type.ToWireName());
    }

    [Fact]
    public void Every_member_has_a_wire_name()
    {
        Assert.Equal(Enum.GetValues<AirQualityAlertType>().Length, WireNames.Count);
    }

    [Fact]
    public void An_unknown_member_throws()
    {
        var ex = Assert.Throws<ArgumentOutOfRangeException>(() => ((AirQualityAlertType)99).ToWireName());
        Assert.Equal("type", ex.ParamName);
    }

    [Fact]
    public void None_is_a_zero_severity_alert_of_the_given_type()
    {
        var none = AirQualityAlert.None(AirQualityAlertType.Pm25);

        Assert.Multiple(
            () => Assert.Equal(AirQualityAlertType.Pm25, none.Type),
            () => Assert.Equal(AlertSeverity.None, none.Severity),
            () => Assert.Equal(0.0, none.TriggerValue),
            () => Assert.Null(none.HoursUntil));
    }
}
