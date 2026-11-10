using System.Collections.Immutable;
using Njord.Core;
using Njord.Grpc.Mapping;
using Njord.Messages.Results.Conditions;

namespace Njord.Grpc.Tests.Mapping;

public sealed class ConditionValueMappingTests
{
    private static readonly Location _vienna = new("Vienna", 48.2, 16.37);

    [Fact]
    public void Condition_result_is_active()
    {
        var start = new DateTimeOffset(2026, 6, 1, 8, 0, 0, TimeSpan.Zero);
        var result = new VentilationWindowResult(_vienna, start, start.AddHours(2));

        Assert.True(result.ToConditionValue().Active);
    }

    [Fact]
    public void Heat_pump_cop_maps_value_and_extras()
    {
        var result = new HeatPumpCopConditionResult(_vienna, 3.8, HeatPumpRecommendation.Ideal, 4.2, 14);

        var value = result.ToConditionValue();

        Assert.Multiple(
            () => Assert.True(value.Active),
            () => Assert.Equal(3.8, value.Extra["value"]),
            () => Assert.Equal((double)HeatPumpRecommendation.Ideal, value.Extra["state"]),
            () => Assert.Equal(4.2, value.Extra["best_cop"]),
            () => Assert.Equal(14, value.Extra["best_hour"]));
    }

    [Fact]
    public void Irrigation_need_maps_advice_and_metrics()
    {
        var result = new IrrigationNeedConditionResult(_vienna, IrrigationAdvice.Water, 25.0, 2.5, 4.0);

        var value = result.ToConditionValue();

        Assert.Multiple(
            () => Assert.Equal((double)IrrigationAdvice.Water, value.Extra["state"]),
            () => Assert.Equal(25.0, value.Extra["soil_moisture_percent"]),
            () => Assert.Equal(2.5, value.Extra["rain_expected_mm"]),
            () => Assert.Equal(4.0, value.Extra["et_mm"]));
    }

    [Fact]
    public void Frost_depth_maps_depth_and_risk()
    {
        var result = new FrostDepthConditionResult(_vienna, 15.0, FrostDepthRisk.Moderate, 5.0, -2.5);

        var value = result.ToConditionValue();

        Assert.Multiple(
            () => Assert.Equal(15.0, value.Extra["value"]),
            () => Assert.Equal((double)FrostDepthRisk.Moderate, value.Extra["state"]),
            () => Assert.Equal(5.0, value.Extra["snow_insulation_cm"]),
            () => Assert.Equal(-2.5, value.Extra["soil_temperature_c"]));
    }

    [Fact]
    public void Inversion_detection_maps_strength_and_optional_type()
    {
        var result = new InversionDetectionConditionResult(_vienna, InversionStrength.Strong, InversionType.Radiation, 8.5, 500.0);

        var value = result.ToConditionValue();

        Assert.Multiple(
            () => Assert.Equal((double)InversionStrength.Strong, value.Extra["state"]),
            () => Assert.Equal(8.5, value.Extra["temperature_difference_c"]),
            () => Assert.Equal(500.0, value.Extra["boundary_layer_height_m"]),
            () => Assert.Equal((double)InversionType.Radiation, value.Extra["inversion_type"]));
    }

    [Fact]
    public void Inversion_detection_omits_null_type()
    {
        var result = new InversionDetectionConditionResult(_vienna, InversionStrength.None, null, 1.0, 1200.0);

        var value = result.ToConditionValue();

        Assert.DoesNotContain("inversion_type", value.Extra.Keys);
    }

    [Fact]
    public void Atmospheric_stability_maps_class_and_metrics()
    {
        var result = new AtmosphericStabilityConditionResult(_vienna, StabilityClass.Unstable, 1500.0, -50.0, -4.0);

        var value = result.ToConditionValue();

        Assert.Multiple(
            () => Assert.Equal((double)StabilityClass.Unstable, value.Extra["state"]),
            () => Assert.Equal(1500.0, value.Extra["cape_j_per_kg"]),
            () => Assert.Equal(-50.0, value.Extra["cin_j_per_kg"]),
            () => Assert.Equal(-4.0, value.Extra["lifted_index"]));
    }

    [Fact]
    public void Cloud_ceiling_maps_height_and_category()
    {
        var result = new CloudCeilingConditionResult(_vienna, CeilingCategory.Low, 300, 80, 40, 10);

        var value = result.ToConditionValue();

        Assert.Multiple(
            () => Assert.Equal(300, value.Extra["value"]),
            () => Assert.Equal((double)CeilingCategory.Low, value.Extra["state"]),
            () => Assert.Equal(80, value.Extra["cloud_cover_low_percent"]),
            () => Assert.Equal(40, value.Extra["cloud_cover_mid_percent"]),
            () => Assert.Equal(10, value.Extra["cloud_cover_high_percent"]));
    }

    [Fact]
    public void Commute_safety_maps_level()
    {
        var result = new CommuteSafetyResult(_vienna, CommuteLevel.Caution, [CommuteHazard.Fog]);

        var value = result.ToConditionValue();

        Assert.Equal((double)CommuteLevel.Caution, value.Extra["state"]);
    }

    [Fact]
    public void Solar_yield_maps_kwh_and_peak_hour()
    {
        var result = new SolarYieldResult(_vienna, 5.2, 13);

        var value = result.ToConditionValue();

        Assert.Multiple(
            () => Assert.Equal(5.2, value.Extra["value"]),
            () => Assert.Equal(13, value.Extra["peak_hour"]));
    }

    [Fact]
    public void Granular_aqi_maps_present_values_and_omits_null()
    {
        var result = new GranularAqiResult(_vienna, 42.0, null, 15.0, null, null);

        var value = result.ToConditionValue();

        Assert.Multiple(
            () => Assert.Equal(42.0, value.Extra["pm25_aqi"]),
            () => Assert.DoesNotContain("pm10_aqi", value.Extra.Keys),
            () => Assert.Equal(15.0, value.Extra["no2_aqi"]),
            () => Assert.DoesNotContain("ozone_aqi", value.Extra.Keys),
            () => Assert.DoesNotContain("so2_aqi", value.Extra.Keys));
    }

    [Fact]
    public void Ocean_currents_maps_velocity_and_optional_direction()
    {
        var result = new OceanCurrentsResult(_vienna, CurrentStrength.Strong, 8.5, 180.0);

        var value = result.ToConditionValue();

        Assert.Multiple(
            () => Assert.Equal(8.5, value.Extra["value"]),
            () => Assert.Equal((double)CurrentStrength.Strong, value.Extra["state"]),
            () => Assert.Equal(180.0, value.Extra["direction"]));
    }

    [Fact]
    public void Ocean_currents_omits_null_direction()
    {
        var result = new OceanCurrentsResult(_vienna, CurrentStrength.Weak, 1.0, null);

        var value = result.ToConditionValue();

        Assert.DoesNotContain("direction", value.Extra.Keys);
    }

    [Fact]
    public void Elevated_wind_maps_speeds_and_optional_120m()
    {
        var result = new ElevatedWindResult(_vienna, 25.0, 270.0, 30.0, 265.0);

        var value = result.ToConditionValue();

        Assert.Multiple(
            () => Assert.Equal(25.0, value.Extra["value"]),
            () => Assert.Equal(270.0, value.Extra["direction_80m"]),
            () => Assert.Equal(30.0, value.Extra["speed_120m"]),
            () => Assert.Equal(265.0, value.Extra["direction_120m"]));
    }

    [Fact]
    public void Elevated_wind_omits_null_120m()
    {
        var result = new ElevatedWindResult(_vienna, 20.0, 180.0, null, null);

        var value = result.ToConditionValue();

        Assert.Multiple(
            () => Assert.DoesNotContain("speed_120m", value.Extra.Keys),
            () => Assert.DoesNotContain("direction_120m", value.Extra.Keys));
    }

    [Fact]
    public void Snow_depth_watch_maps_depth_and_level()
    {
        var result = new SnowDepthWatchResult(_vienna, SnowDepthLevel.Moderate, 15.0);

        var value = result.ToConditionValue();

        Assert.Multiple(
            () => Assert.Equal(15.0, value.Extra["value"]),
            () => Assert.Equal((double)SnowDepthLevel.Moderate, value.Extra["state"]));
    }

    [Fact]
    public void Soil_moisture_maps_level_and_value()
    {
        var result = new SoilMoistureResult(_vienna, SoilMoistureLevel.Optimal, 35.0);

        var value = result.ToConditionValue();

        Assert.Multiple(
            () => Assert.Equal(35.0, value.Extra["value"]),
            () => Assert.Equal((double)SoilMoistureLevel.Optimal, value.Extra["state"]));
    }

    [Fact]
    public void Evapotranspiration_maps_daily_mm_and_level()
    {
        var result = new EvapotranspirationResult(_vienna, EvapotranspirationLevel.High, 6.5);

        var value = result.ToConditionValue();

        Assert.Multiple(
            () => Assert.Equal(6.5, value.Extra["value"]),
            () => Assert.Equal((double)EvapotranspirationLevel.High, value.Extra["state"]));
    }
}
