using System.Text.Json;
using Njord.Messages.Alerts;
using Njord.Mqtt.Payloads;
using Njord.Mqtt.Serialization;

namespace Njord.Mqtt.Tests;

public sealed class MqttPayloadShapeSpec
{
    private static JsonElement Parse(string json) => JsonDocument.Parse(json).RootElement.Clone();

    private static string[] Names(JsonElement element) => [.. element.EnumerateObject().Select(p => p.Name)];

    private static JsonElement Alert(AlertPayload payload)
        => Parse(JsonSerializer.Serialize(payload, MqttPayloadJsonContext.Default.AlertPayload));

    [Theory]
    [InlineData(true, JsonValueKind.True)]
    [InlineData(false, JsonValueKind.False)]
    public void Ice_alert_writes_soil_frozen_as_a_json_boolean(bool soilFrozen, JsonValueKind expectedKind)
    {
        var json = Alert(new IceAlertPayload(-6, 1, soilFrozen) { Severity = AlertSeverity.Red });

        Assert.Multiple(
            () => Assert.Equal(expectedKind, json.GetProperty("soil_frozen").ValueKind),
            () => Assert.Equal(
                ["severity", "confidence", "trigger_value", "threshold", "peak_value", "hours_until", "duration_hours",
                    "expected_low", "rain_hours", "soil_frozen"],
                Names(json)));
    }

    [Fact]
    public void Alert_writes_absent_timing_members_as_json_null_in_fixed_order()
    {
        var json = Alert(new HeatAlertPayload(36, 4) { Severity = AlertSeverity.Orange });

        Assert.Multiple(
            () => Assert.Equal(JsonValueKind.Null, json.GetProperty("peak_value").ValueKind),
            () => Assert.Equal(JsonValueKind.Null, json.GetProperty("hours_until").ValueKind),
            () => Assert.Equal(JsonValueKind.Null, json.GetProperty("duration_hours").ValueKind),
            () => Assert.Equal(JsonValueKind.String, json.GetProperty("severity").ValueKind),
            () => Assert.Equal(JsonValueKind.Number, json.GetProperty("expected_max").ValueKind),
            () => Assert.Equal(
                ["severity", "confidence", "trigger_value", "threshold", "peak_value", "hours_until", "duration_hours",
                    "expected_max", "models_agreeing"],
                Names(json)));
    }

    [Fact]
    public void Alert_writes_present_timing_members_as_numbers()
    {
        var json = Alert(new FogAlertPayload(4, 3)
        {
            Severity = AlertSeverity.Orange,
            PeakValue = 120.5,
            HoursUntil = 2,
            DurationHours = 4,
        });

        Assert.Multiple(
            () => Assert.Equal(120.5, json.GetProperty("peak_value").GetDouble()),
            () => Assert.Equal(2, json.GetProperty("hours_until").GetInt32()),
            () => Assert.Equal(4, json.GetProperty("duration_hours").GetInt32()));
    }

    [Fact]
    public void Alert_nullable_detail_members_are_json_null()
    {
        var snow = Alert(new SnowAlertPayload(2, null, 3) { Severity = AlertSeverity.Yellow });
        var frost = Alert(new FrostAlertPayload(1, null, 0) { Severity = AlertSeverity.None });
        var windChill = Alert(new WindChillAlertPayload(18, 0, null) { Severity = AlertSeverity.None });
        var humidity = Alert(new HumidityAlertPayload(10, null) { Severity = AlertSeverity.None });

        Assert.Multiple(
            () => Assert.Equal(JsonValueKind.Null, snow.GetProperty("freezing_level").ValueKind),
            () => Assert.Equal(JsonValueKind.Null, frost.GetProperty("earliest_frost").ValueKind),
            () => Assert.Equal(JsonValueKind.Null, windChill.GetProperty("exposure_risk").ValueKind),
            () => Assert.Equal(JsonValueKind.Null, humidity.GetProperty("comfort_level").ValueKind));
    }

    [Fact]
    public void Frost_alert_keeps_the_earliest_frost_text()
    {
        var json = Alert(new FrostAlertPayload(-3, "+4h", 2) { Severity = AlertSeverity.Yellow });

        Assert.Equal("+4h", json.GetProperty("earliest_frost").GetString());
    }
}
