using Njord.Forecast;
using Njord.Messages.Alerts;
using Njord.Messages.Pipeline;

namespace Njord.Messages.Tests;

public sealed class LifestyleMappingsSpec
{
    private static readonly DateTimeOffset At = new(2026, 7, 1, 6, 0, 0, TimeSpan.Zero);
    private static readonly Location Lucerne = new("Lucerne", 47.05, 8.31);

    public static TheoryData<PipelineEvent, LifestyleKind, string> Events => new()
    {
        { new PipelineEvent.VentilationAdvised(Lucerne, At, At.AddHours(2), "cool"), LifestyleKind.Ventilation, "ventilation" },
        { new PipelineEvent.ExerciseAdvised(Lucerne, "good", ["mild"], At), LifestyleKind.Exercise, "exercise" },
        { new PipelineEvent.LaundryScoreComputed(Lucerne, 80, At, 4), LifestyleKind.LaundryScore, "laundry_score" },
        { new PipelineEvent.CommuteSafety(Lucerne, "caution", ["ice"]), LifestyleKind.CommuteSafety, "commute_safety" },
        { new PipelineEvent.PollenTrendComputed(Lucerne, [], null, null), LifestyleKind.PollenTrend, "pollen_trend" },
        { new PipelineEvent.NightVentilationAdvised(Lucerne, At, At.AddHours(8), 12, 28), LifestyleKind.NightVentilation, "night_ventilation" },
        { new PipelineEvent.ConfidenceTrend(Lucerne, "rising", 1, 2), LifestyleKind.ConfidenceTrend, "confidence_trend" },
        { new PipelineEvent.SolarYieldComputed(Lucerne, 4.5, 13), LifestyleKind.SolarYield, "solar_yield" },
        { new PipelineEvent.GardeningAdvised(Lucerne, true, false, true), LifestyleKind.Gardening, "gardening" },
        { new PipelineEvent.OutdoorScoreComputed(Lucerne, 70, At, 5), LifestyleKind.OutdoorScore, "outdoor_score" },
        { new PipelineEvent.BBQScoreComputed(Lucerne, 90, At, 3), LifestyleKind.BbqScore, "bbq_score" },
        { new PipelineEvent.FloodTimelineComputed(Lucerne, At, 120.5, 2), LifestyleKind.FloodTimeline, "flood_timeline" },
        { new PipelineEvent.FrontApproaching(Lucerne, TimeSpan.FromHours(6), 4, 5), LifestyleKind.FrontApproaching, "front_approaching" },
        { new PipelineEvent.SurfConditionsComputed(Lucerne, 85, "excellent", 1.8, 14, "offshore", At), LifestyleKind.SurfConditions, "surf_conditions" },
        { new PipelineEvent.SeasonalOutlookComputed(Lucerne, []), LifestyleKind.SeasonalOutlook, "seasonal_outlook" },
        { new PipelineEvent.SatelliteRadiationComputed(Lucerne, At, 450, 380, 3.2), LifestyleKind.SatelliteRadiation, "satellite_radiation" },
    };

    [Theory]
    [MemberData(nameof(Events))]
    public void Every_lifestyle_event_maps_to_its_kind_and_wire_name(PipelineEvent pipelineEvent, LifestyleKind kind, string wireName)
    {
        var update = pipelineEvent.ToLifestyleUpdate();

        Assert.NotNull(update);
        Assert.Multiple(
            () => Assert.Equal(Lucerne, update.Location),
            () => Assert.Equal(kind, update.Kind),
            () => Assert.Equal(wireName, update.Kind.ToWireName()));
    }

    [Fact]
    public void Every_kind_is_covered_by_the_event_rows()
    {
        var kinds = Events.Select(row => row.Data.Item2).Order().ToList();

        Assert.Equal(Enum.GetValues<LifestyleKind>().Order(), kinds);
    }

    [Fact]
    public void An_undefined_kind_has_no_wire_name()
    {
        var ex = Assert.Throws<ArgumentOutOfRangeException>(() => ((LifestyleKind)999).ToWireName());

        Assert.Equal("kind", ex.ParamName);
    }

    [Fact]
    public void Surf_fields_include_the_wind_relation_when_wind_was_considered()
    {
        var update = new PipelineEvent.SurfConditionsComputed(Lucerne, 85, "excellent", 1.8, 14, "offshore", At).ToLifestyleUpdate();

        Assert.NotNull(update);
        Assert.Equal(
            new Dictionary<string, string>
            {
                ["score"] = "85",
                ["rating"] = "excellent",
                ["swell_height"] = "1.8",
                ["swell_period"] = "14",
                ["wind_considered"] = "true",
                ["best_start"] = "2026-07-01T06:00:00Z",
                ["wind"] = "offshore",
            },
            update.Data);
    }

    [Fact]
    public void Surf_fields_omit_the_wind_relation_and_flag_swell_only_results()
    {
        var update = new PipelineEvent.SurfConditionsComputed(Lucerne, 60, "good", 1.8, 14, null, At).ToLifestyleUpdate();

        Assert.NotNull(update);
        Assert.Multiple(
            () => Assert.Equal("false", update.Data["wind_considered"]),
            () => Assert.False(update.Data.ContainsKey("wind")));
    }

    [Fact]
    public void Seasonal_fields_flatten_each_month_with_anomalies_and_confidence()
    {
        var update = new PipelineEvent.SeasonalOutlookComputed(Lucerne,
        [
            new MonthlyOutlook("2026-11", 1.5, -12.5, OutlookConfidence.High),
            new MonthlyOutlook("2026-12", -0.5, 8, OutlookConfidence.Low),
        ]).ToLifestyleUpdate();

        Assert.NotNull(update);
        Assert.Equal(
            new Dictionary<string, string>
            {
                ["month_1"] = "2026-11",
                ["month_1_temp_anomaly"] = "1.5",
                ["month_1_precip_anomaly"] = "-12.5",
                ["month_1_confidence"] = "high",
                ["month_2"] = "2026-12",
                ["month_2_temp_anomaly"] = "-0.5",
                ["month_2_precip_anomaly"] = "8",
                ["month_2_confidence"] = "low",
            },
            update.Data);
    }

    [Fact]
    public void Satellite_fields_carry_the_observed_irradiance_and_the_blended_total()
    {
        var update = new PipelineEvent.SatelliteRadiationComputed(Lucerne, At, 450.5, 380, 3.25).ToLifestyleUpdate();

        Assert.NotNull(update);
        Assert.Equal(
            new Dictionary<string, string>
            {
                ["observed_at"] = "2026-07-01T06:00:00Z",
                ["gti"] = "450.5",
                ["ghi"] = "380",
                ["blended_kwh_per_m2"] = "3.25",
            },
            update.Data);
    }

    [Fact]
    public void Satellite_fields_leave_out_what_is_unknown()
    {
        var update = new PipelineEvent.SatelliteRadiationComputed(Lucerne, At, 450, null, null).ToLifestyleUpdate();

        Assert.NotNull(update);
        Assert.Equal(["gti", "observed_at"], update.Data.Keys.Order());
    }

    [Theory]
    [InlineData(OutlookConfidence.High, "high")]
    [InlineData(OutlookConfidence.Medium, "medium")]
    [InlineData(OutlookConfidence.Low, "low")]
    public void Every_outlook_confidence_has_a_wire_name(OutlookConfidence confidence, string wireName)
    {
        Assert.Equal(wireName, confidence.ToWireName());
    }

    [Fact]
    public void An_undefined_outlook_confidence_has_no_wire_name()
    {
        var ex = Assert.Throws<ArgumentOutOfRangeException>(() => ((OutlookConfidence)999).ToWireName());

        Assert.Equal("confidence", ex.ParamName);
    }

    [Fact]
    public void Laundry_fields_are_invariant_text()
    {
        var update = new PipelineEvent.LaundryScoreComputed(Lucerne, 80, At, 4).ToLifestyleUpdate();

        Assert.NotNull(update);
        Assert.Equal(
            new Dictionary<string, string>
            {
                ["score"] = "80",
                ["best_start"] = "2026-07-01T06:00:00Z",
                ["best_hours"] = "4",
            },
            update.Data);
    }

    [Fact]
    public void Pollen_fields_flatten_the_trends_and_omit_an_absent_peak()
    {
        var withPeak = new PipelineEvent.PollenTrendComputed(
            Lucerne, new Dictionary<string, string> { ["birch"] = "rising", ["grass"] = "falling" }, "birch", At).ToLifestyleUpdate();
        var withoutPeak = new PipelineEvent.PollenTrendComputed(
            Lucerne, new Dictionary<string, string> { ["birch"] = "stable" }, null, null).ToLifestyleUpdate();

        Assert.NotNull(withPeak);
        Assert.NotNull(withoutPeak);
        Assert.Multiple(
            () => Assert.Equal(
                new Dictionary<string, string>
                {
                    ["trend_birch"] = "rising",
                    ["trend_grass"] = "falling",
                    ["peak_type"] = "birch",
                    ["peak_time"] = "2026-07-01T06:00:00Z",
                },
                withPeak.Data),
            () => Assert.Equal(new Dictionary<string, string> { ["trend_birch"] = "stable" }, withoutPeak.Data));
    }

    [Fact]
    public void Gardening_front_and_list_fields_use_lowercase_flags_hours_and_commas()
    {
        var gardening = new PipelineEvent.GardeningAdvised(Lucerne, true, false, true).ToLifestyleUpdate();
        var front = new PipelineEvent.FrontApproaching(Lucerne, TimeSpan.FromHours(6.5), 4.25, 5).ToLifestyleUpdate();
        var commute = new PipelineEvent.CommuteSafety(Lucerne, "caution", ["ice", "wind"]).ToLifestyleUpdate();

        Assert.NotNull(gardening);
        Assert.NotNull(front);
        Assert.NotNull(commute);
        Assert.Multiple(
            () => Assert.Equal(
                new Dictionary<string, string> { ["planting_safe"] = "true", ["watering_needed"] = "false", ["frost_risk"] = "true" },
                gardening.Data),
            () => Assert.Equal(
                new Dictionary<string, string> { ["estimated_in_hours"] = "6.5", ["pressure_drop"] = "4.25", ["temp_drop"] = "5" },
                front.Data),
            () => Assert.Equal("ice,wind", commute.Data["risks"]));
    }

    [Fact]
    public void Non_lifestyle_events_map_to_nothing()
    {
        Assert.Null(new PipelineEvent.AlertRaised(Lucerne, new Alert(AlertType.Heat, AlertSeverity.Yellow, 0.75, NoDetails.Instance), At).ToLifestyleUpdate());
    }
}
