using Njord.Messages.Alerts;
using Njord.Model;
using Njord.Mqtt.Mapping;
using Njord.Mqtt.Payloads;

namespace Njord.Mqtt.Tests.Mapping;

public sealed class AnalysisPayloadMappingsSpec
{
    private static Alert AlertWith(AlertType type, AlertDetails details) =>
        new(type, AlertSeverity.Orange, 0.8, details, TriggerValue: 1.5, Threshold: 2.5, PeakValue: 3.5, HoursUntil: 4, DurationHours: 5);

    private static void AssertCommonMembers(AlertPayload payload) =>
        Assert.Multiple(
            () => Assert.Equal(AlertSeverity.Orange, payload.Severity),
            () => Assert.Equal(0.8, payload.Confidence),
            () => Assert.Equal(1.5, payload.TriggerValue),
            () => Assert.Equal(2.5, payload.Threshold),
            () => Assert.Equal(3.5, payload.PeakValue),
            () => Assert.Equal(4, payload.HoursUntil),
            () => Assert.Equal(5, payload.DurationHours));

    [Fact]
    public void Alert_without_details_maps_only_the_common_members()
    {
        var payload = Assert.IsType<NoDetailsAlertPayload>(Alert.None(AlertType.Heat).ToPayload());

        Assert.Multiple(
            () => Assert.Equal(AlertSeverity.None, payload.Severity),
            () => Assert.Equal(0.0, payload.Confidence),
            () => Assert.Equal(0.0, payload.TriggerValue),
            () => Assert.Equal(0.0, payload.Threshold),
            () => Assert.Null(payload.PeakValue),
            () => Assert.Null(payload.HoursUntil),
            () => Assert.Null(payload.DurationHours));
    }

    [Fact]
    public void Frost_alert_maps_the_earliest_frost_as_offset_text()
    {
        var payload = Assert.IsType<FrostAlertPayload>(
            AlertWith(AlertType.Frost, new FrostDetails(-2.1, Horizon.Hours(4), 3)).ToPayload());

        AssertCommonMembers(payload);
        Assert.Multiple(
            () => Assert.Equal(-2.1, payload.ExpectedLow),
            () => Assert.Equal("+4h", payload.EarliestFrost),
            () => Assert.Equal(3, payload.ModelsAgreeing));
    }

    [Fact]
    public void Frost_alert_without_earliest_frost_maps_null()
    {
        var payload = Assert.IsType<FrostAlertPayload>(
            AlertWith(AlertType.Frost, new FrostDetails(-2.1, null, 3)).ToPayload());

        Assert.Null(payload.EarliestFrost);
    }

    [Fact]
    public void Heat_alert_maps_its_members()
    {
        var payload = Assert.IsType<HeatAlertPayload>(AlertWith(AlertType.Heat, new HeatDetails(34.5, 4)).ToPayload());

        AssertCommonMembers(payload);
        Assert.Multiple(
            () => Assert.Equal(34.5, payload.ExpectedMax),
            () => Assert.Equal(4, payload.ModelsAgreeing));
    }

    [Fact]
    public void Storm_alert_maps_its_members()
    {
        var payload = Assert.IsType<StormAlertPayload>(AlertWith(AlertType.Storm, new StormDetails(27.3, 2)).ToPayload());

        AssertCommonMembers(payload);
        Assert.Multiple(
            () => Assert.Equal(27.3, payload.ExpectedMaxGust),
            () => Assert.Equal(2, payload.ModelsAgreeing));
    }

    [Fact]
    public void Heavy_rain_alert_maps_its_members()
    {
        var payload = Assert.IsType<HeavyRainAlertPayload>(
            AlertWith(AlertType.HeavyRain, new HeavyRainDetails(3, 2)).ToPayload());

        AssertCommonMembers(payload);
        Assert.Multiple(
            () => Assert.Equal(3, payload.HourlyExceedModels),
            () => Assert.Equal(2, payload.DailyExceedModels));
    }

    [Fact]
    public void Uv_alert_maps_its_members()
    {
        var payload = Assert.IsType<UvAlertPayload>(AlertWith(AlertType.Uv, new UvDetails(UvLevel.VeryHigh, 9.5)).ToPayload());

        AssertCommonMembers(payload);
        Assert.Multiple(
            () => Assert.Equal(UvLevel.VeryHigh, payload.UvLevel),
            () => Assert.Equal(9.5, payload.UvIndex));
    }

    [Fact]
    public void Fog_alert_maps_its_members()
    {
        var payload = Assert.IsType<FogAlertPayload>(AlertWith(AlertType.Fog, new FogDetails(6, 3)).ToPayload());

        AssertCommonMembers(payload);
        Assert.Multiple(
            () => Assert.Equal(6, payload.FogHours),
            () => Assert.Equal(3, payload.ModelsAgreeing));
    }

    [Fact]
    public void Snow_alert_maps_its_members()
    {
        var payload = Assert.IsType<SnowAlertPayload>(AlertWith(AlertType.Snow, new SnowDetails(12.5, 800.0, 3)).ToPayload());

        AssertCommonMembers(payload);
        Assert.Multiple(
            () => Assert.Equal(12.5, payload.ExpectedAccumulation),
            () => Assert.Equal(800.0, payload.FreezingLevel),
            () => Assert.Equal(3, payload.ModelsAgreeing));
    }

    [Fact]
    public void Snow_alert_without_freezing_level_maps_null()
    {
        var payload = Assert.IsType<SnowAlertPayload>(AlertWith(AlertType.Snow, new SnowDetails(12.5, null, 3)).ToPayload());

        Assert.Null(payload.FreezingLevel);
    }

    [Fact]
    public void Pressure_drop_alert_maps_its_members()
    {
        var payload = Assert.IsType<PressureDropAlertPayload>(
            AlertWith(AlertType.PressureDrop, new PressureDropDetails(8.5, 2)).ToPayload());

        AssertCommonMembers(payload);
        Assert.Multiple(
            () => Assert.Equal(8.5, payload.MaxDrop),
            () => Assert.Equal(2, payload.ModelsAgreeing));
    }

    [Fact]
    public void Thunderstorm_alert_maps_its_members()
    {
        var payload = Assert.IsType<ThunderstormAlertPayload>(
            AlertWith(AlertType.Thunderstorm, new ThunderstormDetails(4)).ToPayload());

        AssertCommonMembers(payload);
        Assert.Equal(4, payload.ModelsAgreeing);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Ice_alert_maps_its_members(bool soilFrozen)
    {
        var payload = Assert.IsType<IceAlertPayload>(AlertWith(AlertType.Ice, new IceDetails(-1.5, 2, soilFrozen)).ToPayload());

        AssertCommonMembers(payload);
        Assert.Multiple(
            () => Assert.Equal(-1.5, payload.ExpectedLow),
            () => Assert.Equal(2, payload.RainHours),
            () => Assert.Equal(soilFrozen, payload.SoilFrozen));
    }

    [Fact]
    public void Wind_chill_alert_maps_its_members()
    {
        var payload = Assert.IsType<WindChillAlertPayload>(
            AlertWith(AlertType.WindChill, new WindChillDetails(-18.5, 1.4, ExposureRisk.Frostbite10Min)).ToPayload());

        AssertCommonMembers(payload);
        Assert.Multiple(
            () => Assert.Equal(-18.5, payload.FeltTemperature),
            () => Assert.Equal(1.4, payload.WindFactor),
            () => Assert.Equal(ExposureRisk.Frostbite10Min, payload.ExposureRisk));
    }

    [Fact]
    public void Wind_chill_alert_without_exposure_risk_maps_null()
    {
        var payload = Assert.IsType<WindChillAlertPayload>(
            AlertWith(AlertType.WindChill, new WindChillDetails(-18.5, 1.4, null)).ToPayload());

        Assert.Null(payload.ExposureRisk);
    }

    [Fact]
    public void Visibility_alert_maps_its_members()
    {
        var payload = Assert.IsType<VisibilityAlertPayload>(
            AlertWith(AlertType.Visibility, new VisibilityDetails(150.0, 5)).ToPayload());

        AssertCommonMembers(payload);
        Assert.Multiple(
            () => Assert.Equal(150.0, payload.MinVisibility),
            () => Assert.Equal(5, payload.HoursBelow1000m));
    }

    [Fact]
    public void Tropical_night_alert_maps_its_members()
    {
        var payload = Assert.IsType<TropicalNightAlertPayload>(
            AlertWith(AlertType.TropicalNight, new TropicalNightDetails(21.5, 7)).ToPayload());

        AssertCommonMembers(payload);
        Assert.Multiple(
            () => Assert.Equal(21.5, payload.NightMinimum),
            () => Assert.Equal(7, payload.HoursAbove20));
    }

    [Fact]
    public void Humidity_alert_maps_its_members()
    {
        var payload = Assert.IsType<HumidityAlertPayload>(
            AlertWith(AlertType.Humidity, new HumidityDetails(19.5, HumidityComfort.Oppressive)).ToPayload());

        AssertCommonMembers(payload);
        Assert.Multiple(
            () => Assert.Equal(19.5, payload.MaxDewpoint),
            () => Assert.Equal(HumidityComfort.Oppressive, payload.ComfortLevel));
    }

    [Fact]
    public void Humidity_alert_without_comfort_level_maps_null()
    {
        var payload = Assert.IsType<HumidityAlertPayload>(
            AlertWith(AlertType.Humidity, new HumidityDetails(19.5, null)).ToPayload());

        Assert.Null(payload.ComfortLevel);
    }

    [Fact]
    public void Alert_without_timing_maps_absent_timing_members_to_null()
    {
        var alert = new Alert(AlertType.Heat, AlertSeverity.Yellow, 0.5, new HeatDetails(30.0, 2));

        var payload = alert.ToPayload();

        Assert.Multiple(
            () => Assert.Null(payload.PeakValue),
            () => Assert.Null(payload.HoursUntil),
            () => Assert.Null(payload.DurationHours));
    }
}
