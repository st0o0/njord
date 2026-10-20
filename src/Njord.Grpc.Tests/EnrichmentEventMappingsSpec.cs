using Google.Protobuf.WellKnownTypes;
using Njord.Forecast;
using Njord.Grpc.Mapping;
using Njord.Messages.Alerts;
using Njord.Messages.Pipeline;
using Njord.Tests.Shared;
using Case = Njord.Grpc.V2.EnrichmentEvent.FeatureOneofCase;

namespace Njord.Grpc.Tests;

public sealed class EnrichmentEventMappingsSpec
{
    private static readonly DateTimeOffset Anchor = TestTimeConstants.BudgetEpoch;
    private static readonly DateTimeOffset Stamp = Anchor.AddHours(1);
    private static readonly Location Lucerne = DomainData.Lucerne;

    private static V2.EnrichmentEvent Map(FeatureResult result)
    {
        var evt = result.ToEnrichmentEvent(Stamp);
        Assert.NotNull(evt);
        Assert.Equal(Lucerne.Name, evt.Location);
        Assert.Equal(Stamp.ToTimestamp(), evt.UpdatedAt);
        return evt;
    }

    private static void AssertAlert(V2.AlertData alert)
    {
        Assert.Multiple(
            () => Assert.Equal(V2.AlertSeverity.Orange, alert.Severity),
            () => Assert.Equal(0.8, alert.Confidence),
            () => Assert.Equal(10.0, alert.TriggerValue),
            () => Assert.Equal(5.0, alert.Threshold),
            () => Assert.Equal(12.0, alert.PeakValue),
            () => Assert.Equal(2, alert.HoursUntil),
            () => Assert.Equal(3, alert.DurationHours),
            () => Assert.Equal(V2.ConfidenceLevel.High, alert.ConfidenceLevel));
    }

    private static void AssertScore(V2.ScoreData score, int expectedScore)
    {
        Assert.Multiple(
            () => Assert.Equal(expectedScore, score.Score),
            () => Assert.Equal(Anchor.ToTimestamp(), score.BestStart),
            () => Assert.Equal(3, score.BestHours));
    }

    [Fact]
    public void Optional_alert_values_stay_unset_when_absent()
    {
        var evt = Map(new HeatAlertResult(Lucerne, AlertSeverity.Yellow, 0.5, 1.0, 2.0, null, null, null, ConfidenceLevel.Low, 3.0));

        Assert.Multiple(
            () => Assert.False(evt.Heat.Alert.HasPeakValue),
            () => Assert.False(evt.Heat.Alert.HasHoursUntil),
            () => Assert.False(evt.Heat.Alert.HasDurationHours),
            () => Assert.Equal(V2.ConfidenceLevel.Low, evt.Heat.Alert.ConfidenceLevel));
    }

    [Fact]
    public void Frost_alerts_map_to_the_frost_case()
    {
        var evt = Map(new FrostAlertResult(Lucerne, AlertSeverity.Orange, 0.8, 10.0, 5.0, 12.0, 2, 3, ConfidenceLevel.High, -4.0, 5));

        Assert.Equal(Case.Frost, evt.FeatureCase);
        AssertAlert(evt.Frost.Alert);
        Assert.Equal(-4.0, evt.Frost.ExpectedLow);
        Assert.Equal(5, evt.Frost.EarliestFrostHours);
    }

    [Fact]
    public void Heat_alerts_map_to_the_heat_case()
    {
        var evt = Map(new HeatAlertResult(Lucerne, AlertSeverity.Orange, 0.8, 10.0, 5.0, 12.0, 2, 3, ConfidenceLevel.High, 35.5));

        Assert.Equal(Case.Heat, evt.FeatureCase);
        AssertAlert(evt.Heat.Alert);
        Assert.Equal(35.5, evt.Heat.ExpectedMax);
    }

    [Fact]
    public void Storm_alerts_map_to_the_storm_case()
    {
        var evt = Map(new StormAlertResult(Lucerne, AlertSeverity.Orange, 0.8, 10.0, 5.0, 12.0, 2, 3, ConfidenceLevel.High, 28.0));

        Assert.Equal(Case.Storm, evt.FeatureCase);
        AssertAlert(evt.Storm.Alert);
        Assert.Equal(28.0, evt.Storm.ExpectedMaxGust);
    }

    [Fact]
    public void Heavy_rain_alerts_map_to_the_heavy_rain_case()
    {
        var evt = Map(new HeavyRainAlertResult(Lucerne, AlertSeverity.Orange, 0.8, 10.0, 5.0, 12.0, 2, 3, ConfidenceLevel.High, 4, 6));

        Assert.Equal(Case.HeavyRain, evt.FeatureCase);
        AssertAlert(evt.HeavyRain.Alert);
        Assert.Equal(4, evt.HeavyRain.HourlyExceedModels);
        Assert.Equal(6, evt.HeavyRain.DailyExceedModels);
    }

    [Fact]
    public void Uv_alerts_map_to_the_uv_case()
    {
        var evt = Map(new UvAlertResult(Lucerne, AlertSeverity.Orange, 0.8, 10.0, 5.0, 12.0, 2, 3, ConfidenceLevel.High, UvLevel.VeryHigh, 9.5));

        Assert.Equal(Case.Uv, evt.FeatureCase);
        AssertAlert(evt.Uv.Alert);
        Assert.Equal(V2.UvLevel.VeryHigh, evt.Uv.UvLevel);
        Assert.Equal(9.5, evt.Uv.UvIndex);
    }

    [Fact]
    public void Fog_alerts_map_to_the_fog_case()
    {
        var evt = Map(new FogAlertResult(Lucerne, AlertSeverity.Orange, 0.8, 10.0, 5.0, 12.0, 2, 3, ConfidenceLevel.High, 7));

        Assert.Equal(Case.Fog, evt.FeatureCase);
        AssertAlert(evt.Fog.Alert);
        Assert.Equal(7, evt.Fog.FogHours);
    }

    [Fact]
    public void Snow_alerts_map_to_the_snow_case()
    {
        var evt = Map(new SnowAlertResult(Lucerne, AlertSeverity.Orange, 0.8, 10.0, 5.0, 12.0, 2, 3, ConfidenceLevel.High, 15.0, 800.0));

        Assert.Equal(Case.Snow, evt.FeatureCase);
        AssertAlert(evt.Snow.Alert);
        Assert.Equal(15.0, evt.Snow.ExpectedAccumulation);
        Assert.Equal(800.0, evt.Snow.FreezingLevel);
    }

    [Fact]
    public void Snow_alerts_without_a_freezing_level_leave_it_unset()
    {
        var evt = Map(new SnowAlertResult(Lucerne, AlertSeverity.Orange, 0.8, 10.0, 5.0, 12.0, 2, 3, ConfidenceLevel.High, 15.0, null));

        Assert.False(evt.Snow.HasFreezingLevel);
    }

    [Fact]
    public void Pressure_drop_alerts_map_to_the_pressure_drop_case()
    {
        var evt = Map(new PressureDropAlertResult(Lucerne, AlertSeverity.Orange, 0.8, 10.0, 5.0, 12.0, 2, 3, ConfidenceLevel.High, 11.0));

        Assert.Equal(Case.PressureDrop, evt.FeatureCase);
        AssertAlert(evt.PressureDrop.Alert);
        Assert.Equal(11.0, evt.PressureDrop.MaxDrop);
    }

    [Fact]
    public void Thunderstorm_alerts_map_to_the_thunderstorm_case()
    {
        var evt = Map(new ThunderstormAlertResult(Lucerne, AlertSeverity.Orange, 0.8, 10.0, 5.0, 12.0, 2, 3, ConfidenceLevel.High));

        Assert.Equal(Case.Thunderstorm, evt.FeatureCase);
        AssertAlert(evt.Thunderstorm.Alert);
    }

    [Fact]
    public void Ice_alerts_map_to_the_ice_case()
    {
        var evt = Map(new IceAlertResult(Lucerne, AlertSeverity.Orange, 0.8, 10.0, 5.0, 12.0, 2, 3, ConfidenceLevel.High, -2.0, 3, true));

        Assert.Equal(Case.Ice, evt.FeatureCase);
        AssertAlert(evt.Ice.Alert);
        Assert.Equal(-2.0, evt.Ice.ExpectedLow);
        Assert.Equal(3, evt.Ice.RainHours);
        Assert.True(evt.Ice.SoilFrozen);
    }

    [Fact]
    public void Wind_chill_alerts_map_to_the_wind_chill_case()
    {
        var evt = Map(new WindChillAlertResult(Lucerne, AlertSeverity.Orange, 0.8, 10.0, 5.0, 12.0, 2, 3, ConfidenceLevel.High, -20.0, 1.5, ExposureRisk.Frostbite10Minutes));

        Assert.Equal(Case.WindChill, evt.FeatureCase);
        AssertAlert(evt.WindChill.Alert);
        Assert.Equal(-20.0, evt.WindChill.FeltTemperature);
        Assert.Equal(1.5, evt.WindChill.WindFactor);
        Assert.Equal(V2.ExposureRisk.Frostbite10Minutes, evt.WindChill.ExposureRisk);
    }

    [Fact]
    public void Wind_chill_alerts_without_an_exposure_risk_leave_it_unset()
    {
        var evt = Map(new WindChillAlertResult(Lucerne, AlertSeverity.Orange, 0.8, 10.0, 5.0, 12.0, 2, 3, ConfidenceLevel.High, -20.0, 1.5, null));

        Assert.False(evt.WindChill.HasExposureRisk);
    }

    [Fact]
    public void Visibility_alerts_map_to_the_visibility_case()
    {
        var evt = Map(new VisibilityAlertResult(Lucerne, AlertSeverity.Orange, 0.8, 10.0, 5.0, 12.0, 2, 3, ConfidenceLevel.High, 150.0, 5));

        Assert.Equal(Case.Visibility, evt.FeatureCase);
        AssertAlert(evt.Visibility.Alert);
        Assert.Equal(150.0, evt.Visibility.MinVisibility);
        Assert.Equal(5, evt.Visibility.HoursBelow1000M);
    }

    [Fact]
    public void Tropical_night_alerts_map_to_the_tropical_night_case()
    {
        var evt = Map(new TropicalNightAlertResult(Lucerne, AlertSeverity.Orange, 0.8, 10.0, 5.0, 12.0, 2, 3, ConfidenceLevel.High, 21.5, 6));

        Assert.Equal(Case.TropicalNight, evt.FeatureCase);
        AssertAlert(evt.TropicalNight.Alert);
        Assert.Equal(21.5, evt.TropicalNight.NightMinimum);
        Assert.Equal(6, evt.TropicalNight.HoursAbove20);
    }

    [Fact]
    public void Humidity_alerts_map_to_the_humidity_case()
    {
        var evt = Map(new HumidityAlertResult(Lucerne, AlertSeverity.Orange, 0.8, 10.0, 5.0, 12.0, 2, 3, ConfidenceLevel.High, 19.0, ComfortLevel.Oppressive));

        Assert.Equal(Case.Humidity, evt.FeatureCase);
        AssertAlert(evt.Humidity.Alert);
        Assert.Equal(19.0, evt.Humidity.MaxDewpoint);
        Assert.Equal(V2.ComfortLevel.Oppressive, evt.Humidity.ComfortLevel);
    }

    [Fact]
    public void Humidity_alerts_without_a_comfort_level_leave_it_unset()
    {
        var evt = Map(new HumidityAlertResult(Lucerne, AlertSeverity.Orange, 0.8, 10.0, 5.0, 12.0, 2, 3, ConfidenceLevel.High, 19.0, null));

        Assert.False(evt.Humidity.HasComfortLevel);
    }

    [Fact]
    public void Pollen_alerts_map_to_the_pollen_case()
    {
        var evt = Map(new PollenAlertResult(Lucerne, AlertSeverity.Orange, 0.8, 10.0, 5.0, 12.0, 2, 3, ConfidenceLevel.High));

        Assert.Equal(Case.Pollen, evt.FeatureCase);
        AssertAlert(evt.Pollen.Alert);
    }

    [Fact]
    public void Pm25_alerts_map_to_the_pm25_case()
    {
        var evt = Map(new Pm25AlertResult(Lucerne, AlertSeverity.Orange, 0.8, 10.0, 5.0, 12.0, 2, 3, ConfidenceLevel.High));

        Assert.Equal(Case.Pm25, evt.FeatureCase);
        AssertAlert(evt.Pm25.Alert);
    }

    [Fact]
    public void Dust_alerts_map_to_the_dust_case()
    {
        var evt = Map(new DustAlertResult(Lucerne, AlertSeverity.Orange, 0.8, 10.0, 5.0, 12.0, 2, 3, ConfidenceLevel.High));

        Assert.Equal(Case.Dust, evt.FeatureCase);
        AssertAlert(evt.Dust.Alert);
    }

    [Fact]
    public void Flood_risk_alerts_map_to_the_flood_risk_case()
    {
        var evt = Map(new FloodRiskAlertResult(Lucerne, AlertSeverity.Orange, 0.8, 10.0, 5.0, 12.0, 2, 3, ConfidenceLevel.High));

        Assert.Equal(Case.FloodRisk, evt.FeatureCase);
        AssertAlert(evt.FloodRisk.Alert);
    }

    [Fact]
    public void Flood_rain_alerts_map_to_the_flood_rain_case()
    {
        var evt = Map(new FloodRainAlertResult(Lucerne, AlertSeverity.Orange, 0.8, 10.0, 5.0, 12.0, 2, 3, ConfidenceLevel.High));

        Assert.Equal(Case.FloodRain, evt.FeatureCase);
        AssertAlert(evt.FloodRain.Alert);
    }

    [Fact]
    public void Swimming_conditions_alerts_map_to_the_swimming_conditions_case()
    {
        var evt = Map(new SwimmingConditionsAlertResult(Lucerne, AlertSeverity.Orange, 0.8, 10.0, 5.0, 12.0, 2, 3, ConfidenceLevel.High));

        Assert.Equal(Case.SwimmingConditions, evt.FeatureCase);
        AssertAlert(evt.SwimmingConditions.Alert);
    }

    [Fact]
    public void Bbq_scores_map_to_the_bbq_case()
    {
        var evt = Map(new BbqScoreResult(Lucerne, 71, Anchor, 3));

        Assert.Equal(Case.Bbq, evt.FeatureCase);
        AssertScore(evt.Bbq.Score, 71);
    }

    [Fact]
    public void Laundry_scores_map_to_the_laundry_case()
    {
        var evt = Map(new LaundryScoreResult(Lucerne, 72, Anchor, 3));

        Assert.Equal(Case.Laundry, evt.FeatureCase);
        AssertScore(evt.Laundry.Score, 72);
    }

    [Fact]
    public void Outdoor_scores_map_to_the_outdoor_case()
    {
        var evt = Map(new OutdoorScoreResult(Lucerne, 73, Anchor, 3));

        Assert.Equal(Case.Outdoor, evt.FeatureCase);
        AssertScore(evt.Outdoor.Score, 73);
    }

    [Fact]
    public void Surf_conditions_scores_map_to_the_surf_conditions_case()
    {
        var evt = Map(new SurfConditionsResult(Lucerne, 74, Anchor, 3));

        Assert.Equal(Case.SurfConditions, evt.FeatureCase);
        AssertScore(evt.SurfConditions.Score, 74);
    }

    [Fact]
    public void Commute_safety_maps_to_the_commute_safety_case()
    {
        var evt = Map(new CommuteSafetyResult(Lucerne, CommuteLevel.Dangerous, [CommuteHazard.Ice, CommuteHazard.Fog]));

        Assert.Equal(Case.CommuteSafety, evt.FeatureCase);
        Assert.Equal(V2.CommuteLevel.Dangerous, evt.CommuteSafety.Level);
        Assert.Equal([V2.CommuteHazard.Ice, V2.CommuteHazard.Fog], evt.CommuteSafety.Hazards);
    }

    [Fact]
    public void Ventilation_windows_map_to_the_ventilation_window_case()
    {
        var evt = Map(new VentilationWindowResult(Lucerne, Anchor, Anchor.AddHours(5)));

        Assert.Equal(Case.VentilationWindow, evt.FeatureCase);
        Assert.Equal(Anchor.ToTimestamp(), evt.VentilationWindow.BestStart);
        Assert.Equal(Anchor.AddHours(5).ToTimestamp(), evt.VentilationWindow.BestEnd);
    }

    [Fact]
    public void Night_ventilation_maps_to_the_night_ventilation_case()
    {
        var evt = Map(new NightVentilationResult(Lucerne, Anchor, Anchor.AddHours(8), 14.5, 31.0));

        Assert.Equal(Case.NightVentilation, evt.FeatureCase);
        Assert.Equal(Anchor.ToTimestamp(), evt.NightVentilation.OpenAt);
        Assert.Equal(Anchor.AddHours(8).ToTimestamp(), evt.NightVentilation.CloseAt);
        Assert.Equal(14.5, evt.NightVentilation.LowTemperature);
        Assert.Equal(31.0, evt.NightVentilation.TomorrowPeak);
    }

    [Fact]
    public void Exercise_advisories_map_to_the_exercise_advisory_case()
    {
        var evt = Map(new ExerciseAdvisoryResult(Lucerne, ExerciseLevel.Avoid, [ExerciseFactor.Pm25, ExerciseFactor.Ozone], Anchor.AddHours(4)));

        Assert.Equal(Case.ExerciseAdvisory, evt.FeatureCase);
        Assert.Equal(V2.ExerciseLevel.Avoid, evt.ExerciseAdvisory.Level);
        Assert.Equal([V2.ExerciseFactor.Pm25, V2.ExerciseFactor.Ozone], evt.ExerciseAdvisory.Reasons);
        Assert.Equal(Anchor.AddHours(4).ToTimestamp(), evt.ExerciseAdvisory.BestWindow);
    }

    [Fact]
    public void Gardening_maps_to_the_gardening_case()
    {
        var evt = Map(new GardeningResult(Lucerne, GardeningAdvice.Hold, false, true, true));

        Assert.Equal(Case.Gardening, evt.FeatureCase);
        Assert.Equal(V2.GardeningAdvice.Hold, evt.Gardening.Advice);
        Assert.False(evt.Gardening.PlantingSafe);
        Assert.True(evt.Gardening.WateringNeeded);
        Assert.True(evt.Gardening.FrostRisk);
    }

    [Fact]
    public void Flood_timelines_map_to_the_flood_timeline_case()
    {
        var evt = Map(new FloodTimelineResult(Lucerne, FloodStatus.AboveWarning, Anchor.AddDays(3), 300.0, 2));

        Assert.Equal(Case.FloodTimeline, evt.FeatureCase);
        Assert.Equal(V2.FloodStatus.AboveWarning, evt.FloodTimeline.Status);
        Assert.Equal(Anchor.AddDays(3).ToTimestamp(), evt.FloodTimeline.PeakTime);
        Assert.Equal(300.0, evt.FloodTimeline.PeakDischarge);
        Assert.Equal(2, evt.FloodTimeline.DaysAboveWarning);
    }

    [Fact]
    public void Solar_yield_maps_to_the_solar_yield_case()
    {
        var evt = Map(new SolarYieldResult(Lucerne, 4.2, 13));

        Assert.Equal(Case.SolarYield, evt.FeatureCase);
        Assert.Equal(4.2, evt.SolarYield.KwhPerKwp);
        Assert.Equal(13, evt.SolarYield.PeakHour);
    }

    [Fact]
    public void Satellite_radiation_maps_to_the_satellite_radiation_case()
    {
        var evt = Map(new SatelliteRadiationResult(Lucerne, Anchor, 500.0, 410.0, 3.5));

        Assert.Equal(Case.SatelliteRadiation, evt.FeatureCase);
        Assert.Equal(Anchor.ToTimestamp(), evt.SatelliteRadiation.ObservedAt);
        Assert.Equal(500.0, evt.SatelliteRadiation.Gti);
        Assert.Equal(410.0, evt.SatelliteRadiation.Ghi);
        Assert.Equal(3.5, evt.SatelliteRadiation.BlendedKwhPerSquareMeter);
    }

    [Fact]
    public void Satellite_radiation_without_optional_values_leaves_them_unset()
    {
        var evt = Map(new SatelliteRadiationResult(Lucerne, Anchor, 500.0, null, null));

        Assert.Multiple(
            () => Assert.False(evt.SatelliteRadiation.HasGhi),
            () => Assert.False(evt.SatelliteRadiation.HasBlendedKwhPerSquareMeter));
    }

    [Fact]
    public void Approaching_fronts_map_to_the_approaching_front_case()
    {
        var evt = Map(new ApproachingFrontResult(Lucerne, 2.0, 6.0, 3.0));

        Assert.Equal(Case.ApproachingFront, evt.FeatureCase);
        Assert.Equal(2.0, evt.ApproachingFront.EstimatedInHours);
        Assert.Equal(6.0, evt.ApproachingFront.PressureDrop);
        Assert.Equal(3.0, evt.ApproachingFront.TemperatureDrop);
    }

    [Fact]
    public void Confidence_trends_map_to_the_confidence_trend_case()
    {
        var evt = Map(new ConfidenceTrendResult(Lucerne, ConfidenceTrendDirection.Diverging, 0.4, 1.4, 1.0));

        Assert.Equal(Case.ConfidenceTrend, evt.FeatureCase);
        Assert.Equal(V2.ConfidenceTrendDirection.Diverging, evt.ConfidenceTrend.Direction);
        Assert.Equal(0.4, evt.ConfidenceTrend.Magnitude);
        Assert.Equal(1.4, evt.ConfidenceTrend.CurrentAvgStdDev);
        Assert.Equal(1.0, evt.ConfidenceTrend.PreviousAvgStdDev);
    }

    [Fact]
    public void Pollen_trends_map_to_the_pollen_trend_case()
    {
        var evt = Map(new PollenTrendResult(Lucerne, PollenType.Grass, Anchor.AddHours(7),
            PollenTrendDirection.Rising, PollenTrendDirection.Stable, PollenTrendDirection.Falling, PollenTrendDirection.Rising));

        Assert.Equal(Case.PollenTrend, evt.FeatureCase);
        Assert.Equal(V2.PollenType.Grass, evt.PollenTrend.PeakType);
        Assert.Equal(Anchor.AddHours(7).ToTimestamp(), evt.PollenTrend.PeakTime);
        Assert.Equal(V2.PollenTrendDirection.Rising, evt.PollenTrend.Birch);
        Assert.Equal(V2.PollenTrendDirection.Stable, evt.PollenTrend.Grass);
        Assert.Equal(V2.PollenTrendDirection.Falling, evt.PollenTrend.Alder);
        Assert.Equal(V2.PollenTrendDirection.Rising, evt.PollenTrend.Mugwort);
    }

    [Fact]
    public void Pollen_trends_without_a_peak_leave_the_optional_values_unset()
    {
        var evt = Map(new PollenTrendResult(Lucerne, null, null, null, null, null, null));

        Assert.Multiple(
            () => Assert.False(evt.PollenTrend.HasPeakType),
            () => Assert.Null(evt.PollenTrend.PeakTime),
            () => Assert.False(evt.PollenTrend.HasBirch),
            () => Assert.False(evt.PollenTrend.HasMugwort));
    }

    [Fact]
    public void Seasonal_outlooks_map_to_the_seasonal_outlook_case_with_one_entry_per_month()
    {
        var evt = Map(new SeasonalOutlookResult(Lucerne,
        [
            new SeasonalMonth(new DateOnly(2026, 11, 1), 1.5, -20.0, ConfidenceLevel.Medium),
            new SeasonalMonth(new DateOnly(2026, 12, 1), -0.5, 35.0, ConfidenceLevel.Low),
        ]));

        Assert.Equal(Case.SeasonalOutlook, evt.FeatureCase);
        Assert.Equal(2, evt.SeasonalOutlook.Months.Count);
        var first = evt.SeasonalOutlook.Months[0];
        var second = evt.SeasonalOutlook.Months[1];
        Assert.Multiple(
            () => Assert.Equal(2026, first.Year),
            () => Assert.Equal(11, first.Month),
            () => Assert.Equal(1.5, first.TemperatureAnomaly),
            () => Assert.Equal(-20.0, first.PrecipitationAnomalyPercent),
            () => Assert.Equal(V2.ConfidenceLevel.Medium, first.Confidence),
            () => Assert.Equal(12, second.Month),
            () => Assert.Equal(V2.ConfidenceLevel.Low, second.Confidence));
    }

    [Fact]
    public void Feature_results_without_a_mapping_are_rejected()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new UnmappedResult(Lucerne).ToEnrichmentEvent(Stamp));
    }

    private sealed record UnmappedResult(Location Location) : FeatureResult(Location);
}
