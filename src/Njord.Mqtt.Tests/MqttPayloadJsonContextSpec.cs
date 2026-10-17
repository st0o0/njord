using System.Text.Json;
using Njord.Compute.Contracts;
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

    [Fact]
    public void Derived_horizon_writes_nulls_explicitly()
    {
        var full = new DerivedHorizonPayload(5, -3.5, DewPointComfort.Sticky, "Clear sky");
        var empty = new DerivedHorizonPayload(null, null, null, null);

        Assert.Multiple(
            () => Assert.Equal(
                "{\"beaufort\":5,\"wind_chill\":-3.5,\"dewpoint_comfort\":\"sticky\",\"wmo_description\":\"Clear sky\"}",
                JsonSerializer.Serialize(full, MqttPayloadJsonContext.Default.DerivedHorizonPayload)),
            () => Assert.Equal(
                "{\"beaufort\":null,\"wind_chill\":null,\"dewpoint_comfort\":null,\"wmo_description\":null}",
                JsonSerializer.Serialize(empty, MqttPayloadJsonContext.Default.DerivedHorizonPayload)));
    }

    [Fact]
    public void Derived_meta_writes_nulls_explicitly()
    {
        Assert.Equal(
            "{\"diurnal_amplitude\":12.3,\"sunshine_pct\":null,\"inversion\":true}",
            JsonSerializer.Serialize(new DerivedMetaPayload(12.3, null, true), MqttPayloadJsonContext.Default.DerivedMetaPayload));
    }

    [Fact]
    public void Index_day_writes_scores_then_hours_then_today_members_then_envelopes()
    {
        var payload = new IndexDayPayload(
            [
                new IndexScorePayload(IndexKind.Laundry, 1, new ScoreEnvelopePayload(0, 5, 0.5)),
                new IndexScorePayload(IndexKind.NightVentilation, 2, null),
                new IndexScorePayload(IndexKind.Bbq, 3, new ScoreEnvelopePayload(1, 9, 1)),
            ],
            9,
            new IndexTodayPayload(7, null, VpdCategory.Optimal, 1.2));

        Assert.Equal(
            "{\"laundry\":1,\"night_ventilation\":2,\"bbq\":3,\"hours_included\":9,"
            + "\"frost_hours\":7,\"frost_confidence\":null,\"vpd_category\":\"optimal\",\"vpd_kpa\":1.2,"
            + "\"laundry_min\":0,\"laundry_max\":5,\"laundry_confidence\":0.5,"
            + "\"bbq_min\":1,\"bbq_max\":9,\"bbq_confidence\":1}",
            JsonSerializer.Serialize(payload, MqttPayloadJsonContext.Default.IndexDayPayload));
    }

    [Fact]
    public void Index_day_without_today_members_omits_them()
    {
        var payload = new IndexDayPayload([new IndexScorePayload(IndexKind.Solar, 4, null)], 3, null);

        Assert.Equal(
            "{\"solar\":4,\"hours_included\":3}",
            JsonSerializer.Serialize(payload, MqttPayloadJsonContext.Default.IndexDayPayload));
    }

    [Fact]
    public void Trend_writes_parameter_entries_before_the_fixed_members()
    {
        var payload = new TrendPayload(
            [
                new TrendParameterPayload(TrendKey.FromApiName("temperature_2m"), TrendDirection.Rising, 1.25),
                new TrendParameterPayload(TrendKey.FromApiName("cloud_cover"), null, null),
            ],
            "Clear to rain", 3, null, 12, 30, Stability.Converging, 0.75, 0.04, null);

        Assert.Equal(
            "{\"trend_temperature_dir\":\"rising\",\"trend_temperature_delta\":1.25,"
            + "\"trend_cloud_cover_dir\":null,\"trend_cloud_cover_delta\":null,"
            + "\"weather_change\":\"Clear to rain\",\"precip_starts\":3,\"precip_ends\":null,"
            + "\"temp_max_in\":12,\"temp_min_in\":30,\"stability\":\"converging\",\"stability_ratio\":0.75,"
            + "\"decay_rate\":0.04,\"reliable_hours\":null}",
            JsonSerializer.Serialize(payload, MqttPayloadJsonContext.Default.TrendPayload));
    }

    [Fact]
    public void Consensus_writes_value_and_models_per_parameter_then_spread_and_agreement()
    {
        var payload = new ConsensusHorizonPayload(
            [
                new ConsensusParameterPayload("temperature", 12.35, 2),
                new ConsensusParameterPayload("wind_speed_10m", null, 0),
            ],
            new ConsensusSpreadPayload(1.23, null));

        Assert.Equal(
            "{\"temperature\":12.35,\"temperature_models\":2,\"wind_speed_10m\":null,\"wind_speed_10m_models\":0,"
            + "\"_spread\":1.23,\"_agreement\":null}",
            JsonSerializer.Serialize(payload, MqttPayloadJsonContext.Default.ConsensusHorizonPayload));
    }

    [Fact]
    public void Consensus_without_spread_omits_spread_and_agreement()
    {
        var payload = new ConsensusHorizonPayload([new ConsensusParameterPayload("temperature", 1, 1)], null);

        Assert.Equal(
            "{\"temperature\":1,\"temperature_models\":1}",
            JsonSerializer.Serialize(payload, MqttPayloadJsonContext.Default.ConsensusHorizonPayload));
    }

    [Fact]
    public void Forecast_horizon_writes_numbers_and_text_and_omits_absent_values()
    {
        var payload = new ForecastHorizonPayload(
            [
                new ForecastHorizonEntryPayload("temperature", 21.5, null),
                new ForecastHorizonEntryPayload("sunrise", null, "05:31"),
                new ForecastHorizonEntryPayload("empty", null, null),
            ]);

        Assert.Equal(
            "{\"temperature\":21.5,\"sunrise\":\"05:31\"}",
            JsonSerializer.Serialize(payload, MqttPayloadJsonContext.Default.ForecastHorizonPayload));
    }
}
