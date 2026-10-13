using Njord.Compute.Consensus;
using Njord.Compute.Contracts;
using Njord.Weather;

namespace Njord.Compute.Tests.Contracts;

public sealed class AlertDetailsSpec
{
    [Fact]
    public void Frost_alert_carries_typed_details()
    {
        var alert = new Alert(AlertType.Frost, AlertSeverity.Yellow, 0.75,
            new FrostDetails(ExpectedLow: -2.1, EarliestFrost: Horizon.Hours(4), ModelsAgreeing: 3));

        var details = Assert.IsType<FrostDetails>(alert.Details);
        Assert.Multiple(
            () => Assert.Equal(AlertType.Frost, alert.Type),
            () => Assert.Equal(AlertSeverity.Yellow, alert.Severity),
            () => Assert.Equal(0.75, alert.Confidence),
            () => Assert.Equal(-2.1, details.ExpectedLow),
            () => Assert.Equal(Horizon.Hours(4), details.EarliestFrost));
    }

    [Fact]
    public void Ice_alert_carries_typed_details()
    {
        var alert = new Alert(AlertType.Ice, AlertSeverity.Orange, 0.9,
            new IceDetails(ExpectedLow: -1.0, RainHours: 4, SoilFrozen: true));

        var details = Assert.IsType<IceDetails>(alert.Details);
        Assert.Multiple(
            () => Assert.Equal(AlertType.Ice, alert.Type),
            () => Assert.Equal(AlertSeverity.Orange, alert.Severity),
            () => Assert.Equal(0.9, alert.Confidence),
            () => Assert.Equal(-1.0, details.ExpectedLow),
            () => Assert.Equal(4, details.RainHours),
            () => Assert.True(details.SoilFrozen));
    }

    [Fact]
    public void None_alert_has_no_details()
    {
        var alert = Alert.None(AlertType.Heat);

        Assert.Same(NoDetails.Instance, alert.Details);
        Assert.Equal(AlertSeverity.None, alert.Severity);
    }

    [Theory]
    [InlineData(UvLevel.Low, "low")]
    [InlineData(UvLevel.Moderate, "moderate")]
    [InlineData(UvLevel.High, "high")]
    [InlineData(UvLevel.VeryHigh, "very_high")]
    [InlineData(UvLevel.Extreme, "extreme")]
    public void Uv_level_has_unchanged_wire_name(UvLevel level, string expected) =>
        Assert.Equal(expected, level.ToWireName());

    [Theory]
    [InlineData(ExposureRisk.Frostbite30Min, "frostbite_30min")]
    [InlineData(ExposureRisk.Frostbite10Min, "frostbite_10min")]
    public void Exposure_risk_has_unchanged_wire_name(ExposureRisk risk, string expected) =>
        Assert.Equal(expected, risk.ToWireName());

    [Theory]
    [InlineData(HumidityComfort.Muggy, "muggy")]
    [InlineData(HumidityComfort.Oppressive, "oppressive")]
    [InlineData(HumidityComfort.Tropical, "tropical")]
    public void Humidity_comfort_has_unchanged_wire_name(HumidityComfort comfort, string expected) =>
        Assert.Equal(expected, comfort.ToWireName());
}
