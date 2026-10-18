using System.Text.Json;
using Njord.Messages.Alerts;
using Njord.Messages.Indices;
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

    [Fact]
    public void Derived_payloads_write_every_member_with_null_for_absent_values()
    {
        var horizon = Parse(JsonSerializer.Serialize(
            new DerivedHorizonPayload(null, -3.5, null, "Clear sky"), MqttPayloadJsonContext.Default.DerivedHorizonPayload));
        var meta = Parse(JsonSerializer.Serialize(
            new DerivedMetaPayload(null, 40.5, false), MqttPayloadJsonContext.Default.DerivedMetaPayload));

        Assert.Multiple(
            () => Assert.Equal(["beaufort", "wind_chill", "dewpoint_comfort", "wmo_description"], Names(horizon)),
            () => Assert.Equal(JsonValueKind.Null, horizon.GetProperty("beaufort").ValueKind),
            () => Assert.Equal(JsonValueKind.Number, horizon.GetProperty("wind_chill").ValueKind),
            () => Assert.Equal(JsonValueKind.Null, horizon.GetProperty("dewpoint_comfort").ValueKind),
            () => Assert.Equal(JsonValueKind.String, horizon.GetProperty("wmo_description").ValueKind),
            () => Assert.Equal(["diurnal_amplitude", "sunshine_pct", "inversion"], Names(meta)),
            () => Assert.Equal(JsonValueKind.Null, meta.GetProperty("diurnal_amplitude").ValueKind),
            () => Assert.Equal(JsonValueKind.False, meta.GetProperty("inversion").ValueKind));
    }

    [Fact]
    public void Index_day_writes_null_for_absent_today_members_and_keeps_order()
    {
        var json = Parse(JsonSerializer.Serialize(
            new IndexDayPayload(
                [new IndexScorePayload(IndexKind.Laundry, 80, new ScoreEnvelopePayload(70, 90, 0.5))],
                12,
                new IndexTodayPayload(null, null, null, null)),
            MqttPayloadJsonContext.Default.IndexDayPayload));

        Assert.Multiple(
            () => Assert.Equal(
                ["laundry", "hours_included", "frost_hours", "frost_confidence", "vpd_category", "vpd_kpa",
                    "laundry_min", "laundry_max", "laundry_confidence"],
                Names(json)),
            () => Assert.Equal(JsonValueKind.Null, json.GetProperty("frost_hours").ValueKind),
            () => Assert.Equal(JsonValueKind.Null, json.GetProperty("frost_confidence").ValueKind),
            () => Assert.Equal(JsonValueKind.Null, json.GetProperty("vpd_category").ValueKind),
            () => Assert.Equal(JsonValueKind.Null, json.GetProperty("vpd_kpa").ValueKind),
            () => Assert.Equal(JsonValueKind.Number, json.GetProperty("laundry").ValueKind));
    }

    [Fact]
    public void Trend_writes_null_for_absent_members_and_keeps_order()
    {
        var json = Parse(JsonSerializer.Serialize(
            new TrendPayload(
                [new TrendParameterPayload(TrendKey.FromApiName("pressure_msl"), null, null)],
                null, null, null, null, null, null, null, null, null),
            MqttPayloadJsonContext.Default.TrendPayload));

        Assert.Multiple(
            () => Assert.Equal(
                ["trend_pressure_msl_dir", "trend_pressure_msl_delta", "weather_change", "precip_starts", "precip_ends",
                    "temp_max_in", "temp_min_in", "stability", "stability_ratio", "decay_rate", "reliable_hours"],
                Names(json)),
            () => Assert.All(json.EnumerateObject(), p => Assert.Equal(JsonValueKind.Null, p.Value.ValueKind)));
    }

    [Fact]
    public void Consensus_writes_null_median_with_model_count_and_null_spread()
    {
        var json = Parse(JsonSerializer.Serialize(
            new ConsensusHorizonPayload(
                [new ConsensusParameterPayload("temperature_2m", null, 0)],
                new ConsensusSpreadPayload(null, null)),
            MqttPayloadJsonContext.Default.ConsensusHorizonPayload));

        Assert.Multiple(
            () => Assert.Equal(["temperature_2m", "temperature_2m_models", "_spread", "_agreement"], Names(json)),
            () => Assert.Equal(JsonValueKind.Null, json.GetProperty("temperature_2m").ValueKind),
            () => Assert.Equal(JsonValueKind.Number, json.GetProperty("temperature_2m_models").ValueKind),
            () => Assert.Equal(JsonValueKind.Null, json.GetProperty("_spread").ValueKind),
            () => Assert.Equal(JsonValueKind.Null, json.GetProperty("_agreement").ValueKind));
    }

    [Fact]
    public void Forecast_horizon_omits_absent_values_and_keeps_numbers_and_text()
    {
        var json = Parse(JsonSerializer.Serialize(
            new ForecastHorizonPayload(
                [
                    new ForecastHorizonEntryPayload("temperature_2m", 21.5, null),
                    new ForecastHorizonEntryPayload("cloud_cover", null, null),
                    new ForecastHorizonEntryPayload("sunrise", null, "05:31"),
                ]),
            MqttPayloadJsonContext.Default.ForecastHorizonPayload));

        Assert.Multiple(
            () => Assert.Equal(["temperature_2m", "sunrise"], Names(json)),
            () => Assert.Equal(JsonValueKind.Number, json.GetProperty("temperature_2m").ValueKind),
            () => Assert.Equal(JsonValueKind.String, json.GetProperty("sunrise").ValueKind));
    }
}
