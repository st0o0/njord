using Njord.Messages.Alerts;
using Njord.Messages.Derived;
using Njord.Messages.Indices;
using Njord.Messages.Trends;
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

    [Fact]
    public void Derived_horizon_maps_members_and_rounds_wind_chill_to_one_digit()
    {
        var payload = new HorizonDerived(5, -3.456, DewPointComfort.Sticky, WeatherCondition.ClearSky).ToPayload();

        Assert.Multiple(
            () => Assert.Equal(5, payload.Beaufort),
            () => Assert.Equal(-3.5, payload.WindChill),
            () => Assert.Equal(DewPointComfort.Sticky, payload.DewpointComfort),
            () => Assert.Equal(WeatherCondition.ClearSky.ToDescription(), payload.WmoDescription));
    }

    [Fact]
    public void Derived_horizon_without_values_maps_all_members_to_null()
    {
        var payload = new HorizonDerived(null, null, null, null).ToPayload();

        Assert.Multiple(
            () => Assert.Null(payload.Beaufort),
            () => Assert.Null(payload.WindChill),
            () => Assert.Null(payload.DewpointComfort),
            () => Assert.Null(payload.WmoDescription));
    }

    [Fact]
    public void Derived_scalars_round_to_one_digit()
    {
        var payload = new ScalarDerived(12.345, 67.89, true).ToPayload();

        Assert.Multiple(
            () => Assert.Equal(12.3, payload.DiurnalAmplitude),
            () => Assert.Equal(67.9, payload.SunshinePct),
            () => Assert.True(payload.Inversion));
    }

    [Fact]
    public void Derived_scalars_without_values_map_to_null()
    {
        var payload = new ScalarDerived(null, null, null).ToPayload();

        Assert.Multiple(
            () => Assert.Null(payload.DiurnalAmplitude),
            () => Assert.Null(payload.SunshinePct),
            () => Assert.Null(payload.Inversion));
    }

    private static readonly ScoreEnvelope Envelope = new(10, 40, 0.65);

    private static DayScoreSet Day(int offset, bool withEnvelopes)
    {
        var env = withEnvelopes ? Envelope : null;
        var scores = new Dictionary<IndexKind, ScoredIndex>
        {
            [IndexKind.Laundry] = new(1, env),
            [IndexKind.Outdoor] = new(2, env),
            [IndexKind.Running] = new(3, null),
            [IndexKind.Cycling] = new(4, null),
            [IndexKind.Bbq] = new(5, null),
            [IndexKind.Irrigation] = new(6, null),
            [IndexKind.Solar] = new(7, null),
            [IndexKind.NightVentilation] = new(8, withEnvelopes ? Envelope : null),
        };
        return new DayScoreSet(offset, scores, 9);
    }

    private static IndexResult IndexResultWith(params DayScoreSet[] days) => new(
        new LocationId("lucerne"),
        days,
        new FrostProtectionInfo(7, 0.9),
        new VpdInfo(VpdCategory.Optimal, 1.2));

    [Fact]
    public void Index_day_zero_carries_scores_hours_and_frost_and_vpd_members()
    {
        var day = Day(0, withEnvelopes: false);

        var payload = day.ToPayload(IndexResultWith(day));

        Assert.Equal(
            [IndexKind.Laundry, IndexKind.Outdoor, IndexKind.Running, IndexKind.Cycling, IndexKind.Bbq,
                IndexKind.Irrigation, IndexKind.Solar, IndexKind.NightVentilation],
            payload.Scores.Select(s => s.Kind));
        Assert.Equal([1, 2, 3, 4, 5, 6, 7, 8], payload.Scores.Select(s => s.Score));
        Assert.All(payload.Scores, s => Assert.Null(s.Envelope));
        var today = Assert.IsType<IndexTodayPayload>(payload.Today);
        Assert.Multiple(
            () => Assert.Equal(9, payload.HoursIncluded),
            () => Assert.Equal(7, today.FrostHours),
            () => Assert.Equal(0.9, today.FrostConfidence),
            () => Assert.Equal(VpdCategory.Optimal, today.VpdCategory),
            () => Assert.Equal(1.2, today.VpdKpa));
    }

    [Fact]
    public void Index_day_zero_without_frost_and_vpd_maps_the_members_to_null()
    {
        var day = Day(0, withEnvelopes: false);

        var payload = day.ToPayload(new IndexResult(new LocationId("lucerne"), [day], null, null));

        var today = Assert.IsType<IndexTodayPayload>(payload.Today);
        Assert.Multiple(
            () => Assert.Null(today.FrostHours),
            () => Assert.Null(today.FrostConfidence),
            () => Assert.Null(today.VpdCategory),
            () => Assert.Null(today.VpdKpa));
    }

    [Fact]
    public void Index_later_days_carry_no_frost_and_vpd_members()
    {
        var day = Day(2, withEnvelopes: false);

        var payload = day.ToPayload(IndexResultWith(day));

        Assert.Null(payload.Today);
    }

    [Fact]
    public void Index_envelopes_map_per_score_where_present()
    {
        var day = Day(1, withEnvelopes: true);

        var payload = day.ToPayload(IndexResultWith(day));

        var withEnvelope = payload.Scores.Where(s => s.Envelope is not null).Select(s => s.Kind).ToList();
        var laundry = Assert.Single(payload.Scores, s => s.Kind == IndexKind.Laundry);
        Assert.Equal([IndexKind.Laundry, IndexKind.Outdoor, IndexKind.NightVentilation], withEnvelope);
        var envelope = Assert.IsType<ScoreEnvelopePayload>(laundry.Envelope);
        Assert.Multiple(
            () => Assert.Equal(10, envelope.Min),
            () => Assert.Equal(40, envelope.Max),
            () => Assert.Equal(0.65, envelope.Confidence));
    }

    [Fact]
    public void Trend_maps_parameter_entries_and_fixed_members()
    {
        var weatherChange = new WeatherChangeResult(WeatherCategory.Clear, WeatherCategory.Rain);
        var result = new TrendResult(
            new LocationId("lucerne"),
            new Dictionary<string, ParameterTrend?>
            {
                ["temperature_2m"] = new ParameterTrend(TrendDirection.Rising, 1.25),
                ["cloud_cover"] = null,
            },
            weatherChange,
            new PrecipTimingInfo(3, 9),
            new ExtremaTimingInfo(12, 30),
            new StabilityInfo(Stability.Converging, 0.75),
            new DecayInfo(0.04, 36));

        var payload = result.ToPayload();

        Assert.Equal(2, payload.Parameters.Count);
        Assert.Multiple(
            () => Assert.Equal("trend_temperature_dir", payload.Parameters[0].Key.DirectionKey),
            () => Assert.Equal(TrendDirection.Rising, payload.Parameters[0].Direction),
            () => Assert.Equal(1.25, payload.Parameters[0].Delta),
            () => Assert.Equal("trend_cloud_cover_delta", payload.Parameters[1].Key.DeltaKey),
            () => Assert.Null(payload.Parameters[1].Direction),
            () => Assert.Null(payload.Parameters[1].Delta),
            () => Assert.Equal(weatherChange.ToDescription(), payload.WeatherChange),
            () => Assert.Equal(3, payload.PrecipStarts),
            () => Assert.Equal(9, payload.PrecipEnds),
            () => Assert.Equal(12, payload.TempMaxIn),
            () => Assert.Equal(30, payload.TempMinIn),
            () => Assert.Equal(Stability.Converging, payload.Stability),
            () => Assert.Equal(0.75, payload.StabilityRatio),
            () => Assert.Equal(0.04, payload.DecayRate),
            () => Assert.Equal(36, payload.ReliableHours));
    }

    [Fact]
    public void Trend_without_optional_results_maps_the_members_to_null()
    {
        var result = new TrendResult(
            new LocationId("lucerne"),
            new Dictionary<string, ParameterTrend?>(),
            null,
            new PrecipTimingInfo(null, null),
            new ExtremaTimingInfo(null, null),
            null,
            null);

        var payload = result.ToPayload();

        Assert.Multiple(
            () => Assert.Empty(payload.Parameters),
            () => Assert.Null(payload.WeatherChange),
            () => Assert.Null(payload.PrecipStarts),
            () => Assert.Null(payload.PrecipEnds),
            () => Assert.Null(payload.TempMaxIn),
            () => Assert.Null(payload.TempMinIn),
            () => Assert.Null(payload.Stability),
            () => Assert.Null(payload.StabilityRatio),
            () => Assert.Null(payload.DecayRate),
            () => Assert.Null(payload.ReliableHours));
    }

    [Theory]
    [InlineData("temperature_2m", "temperature")]
    [InlineData("wind_speed_10m", "wind_speed")]
    [InlineData("precipitation", "precipitation")]
    [InlineData("cloud_cover", "cloud_cover")]
    public void Trend_key_drops_the_measurement_height_from_the_api_name(string apiName, string expected)
    {
        var key = TrendKey.FromApiName(apiName);

        Assert.Multiple(
            () => Assert.Equal(expected, key.Value),
            () => Assert.Equal($"trend_{expected}_dir", key.DirectionKey),
            () => Assert.Equal($"trend_{expected}_delta", key.DeltaKey));
    }

    [Fact]
    public void Trend_key_is_derived_for_every_parameter_the_trend_analyzer_tracks()
    {
        ParameterDef[] tracked =
        [
            ParameterRegistry.Temperature2m,
            ParameterRegistry.WindSpeed10m,
            ParameterRegistry.Precipitation,
            ParameterRegistry.CloudCover,
        ];

        var keys = tracked.Select(p => TrendKey.FromApiName(p.ApiName).Value).ToList();

        Assert.Equal(["temperature", "wind_speed", "precipitation", "cloud_cover"], keys);
    }
}
