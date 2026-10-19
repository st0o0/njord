using System.Text.Json;
using Njord.Messages.Alerts;
using Njord.Mqtt.Payloads;
using Njord.Mqtt.Serialization;

namespace Njord.Mqtt.Tests;

public sealed class MqttPayloadJsonContextSpec
{
    private static string Json(AlertPayload payload) =>
        JsonSerializer.Serialize(payload, MqttPayloadJsonContext.Default.AlertPayload);

    [Fact]
    public void Alert_members_follow_the_common_members_and_write_absent_timing_as_null()
    {
        var payload = new FrostAlertPayload(-2.1, "+4h", 3)
        {
            Severity = AlertSeverity.Yellow,
            Confidence = 0.75,
        };

        Assert.Equal(
            "{\"severity\":\"yellow\",\"confidence\":0.75,\"trigger_value\":0,\"threshold\":0,"
            + "\"peak_value\":null,\"hours_until\":null,\"duration_hours\":null,\"expected_low\":-2.1,\"earliest_frost\":\"\\u002B4h\",\"models_agreeing\":3}",
            Json(payload));
    }

    [Fact]
    public void Alert_timing_members_sit_between_the_common_and_the_detail_members()
    {
        var payload = new UvAlertPayload(UvLevel.VeryHigh, 9.5)
        {
            Severity = AlertSeverity.Red,
            Confidence = 1,
            TriggerValue = 9.5,
            Threshold = 8,
            PeakValue = 10.5,
            HoursUntil = 2,
            DurationHours = 6,
        };

        Assert.Equal(
            "{\"severity\":\"red\",\"confidence\":1,\"trigger_value\":9.5,\"threshold\":8,"
            + "\"peak_value\":10.5,\"hours_until\":2,\"duration_hours\":6,\"uv_level\":\"very_high\",\"uv_index\":9.5}",
            Json(payload));
    }

    [Fact]
    public void Alert_without_details_writes_the_common_and_timing_members()
    {
        var payload = new NoDetailsAlertPayload { Severity = AlertSeverity.None };

        Assert.Equal("{\"severity\":\"none\",\"confidence\":0,\"trigger_value\":0,\"threshold\":0,"
            + "\"peak_value\":null,\"hours_until\":null,\"duration_hours\":null}", Json(payload));
    }

    [Fact]
    public void Nullable_detail_members_are_written_as_null_and_enums_by_wire_name()
    {
        var payload = new WindChillAlertPayload(-18.5, 1.4, null) { Severity = AlertSeverity.Orange };
        var humidity = new HumidityAlertPayload(19.5, HumidityComfort.Oppressive) { Severity = AlertSeverity.Orange };

        Assert.Multiple(
            () => Assert.EndsWith("\"felt_temperature\":-18.5,\"wind_factor\":1.4,\"exposure_risk\":null}", Json(payload)),
            () => Assert.EndsWith("\"max_dewpoint\":19.5,\"comfort_level\":\"oppressive\"}", Json(humidity)));
    }

    [Fact]
    public void Digit_bearing_alert_members_keep_their_wire_names()
    {
        var visibility = new VisibilityAlertPayload(150, 5) { Severity = AlertSeverity.Red };
        var tropical = new TropicalNightAlertPayload(21.5, 7) { Severity = AlertSeverity.Red };

        Assert.Multiple(
            () => Assert.EndsWith("\"min_visibility\":150,\"hours_below_1000m\":5}", Json(visibility)),
            () => Assert.EndsWith("\"night_minimum\":21.5,\"hours_above_20\":7}", Json(tropical)));
    }
}
