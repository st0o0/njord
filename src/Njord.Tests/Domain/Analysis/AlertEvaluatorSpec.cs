using Microsoft.Extensions.Time.Testing;
using Njord.Configuration;
using Njord.Domain.Analysis;
using Njord.Domain.Weather;

namespace Njord.Tests.Domain.Analysis;

public sealed class AlertEvaluatorSpec
{
    private static readonly DateTimeOffset T0 = new(2026, 7, 11, 12, 0, 0, TimeSpan.Zero);
    private static readonly FakeTimeProvider Time = new(T0);
    private static readonly ParameterDef Temperature = ParameterRegistry.GetByApiName("temperature_2m")!;
    private static readonly ParameterDef ApparentTemp = ParameterRegistry.GetByApiName("apparent_temperature")!;
    private static readonly ParameterDef WindGusts = ParameterRegistry.GetByApiName("wind_gusts_10m")!;
    private static readonly ParameterDef Precipitation = ParameterRegistry.GetByApiName("precipitation")!;
    private static readonly ParameterDef UvIndexParam = ParameterRegistry.GetByApiName("uv_index")!;
    private static readonly ParameterDef DewpointParam = ParameterRegistry.GetByApiName("dew_point_2m")!;
    private static readonly ParameterDef WindSpeed = ParameterRegistry.GetByApiName("wind_speed_10m")!;
    private static readonly ParameterDef HumidityParam = ParameterRegistry.GetByApiName("relative_humidity_2m")!;
    private static readonly ParameterDef Snowfall = ParameterRegistry.GetByApiName("snowfall")!;
    private static readonly ParameterDef PressureMsl = ParameterRegistry.GetByApiName("pressure_msl")!;
    private static readonly ParameterDef Cape = ParameterRegistry.GetByApiName("cape")!;
    private static readonly ParameterDef RainParam = ParameterRegistry.GetByApiName("rain")!;
    private static readonly ParameterDef SoilTemp0cm = ParameterRegistry.GetByApiName("soil_temperature_0cm")!;
    private static readonly ParameterDef VisibilityParam = ParameterRegistry.GetByApiName("visibility")!;
    private static readonly ParameterDef IsDayParam = ParameterRegistry.GetByApiName("is_day")!;
    private static readonly ResolvedParameterSet Parameters = ParameterRegistry.Resolve(["Weather", "Solar", "Soil"], [], []);

    private static ConsensusSnapshot ToConsensus(ModelSnapshot snap) =>
        new ConsensusSnapshotFactory(Parameters, Time).Create(snap, "lucerne");

    private static ModelForecast MakeForecast(WeatherModel model, params (ParameterDef Param, double Value)[] hourlyValues)
    {
        var points = new List<ForecastPoint>();
        for (var h = 0; h < 24; h++)
        {
            var values = new Dictionary<ParameterDef, double?>();
            foreach (var (param, value) in hourlyValues)
                values[param] = value;
            points.Add(new ForecastPoint(T0.AddHours(h), values));
        }
        return new ModelForecast(model, "lucerne", new CycleId(T0),
            new ForecastSeries(points), DailyForecastSeries.Empty);
    }

    private static ModelSnapshot SnapshotWith(params ModelForecast[] forecasts)
    {
        var snap = ModelSnapshot.Empty;
        foreach (var f in forecasts) snap = snap.Update(f);
        return snap;
    }

    // --- Frost ---

    [Fact]
    public void Frost_all_models_agree()
    {
        var snap = SnapshotWith(
            MakeForecast(new("m1"), (Temperature, -2.0)),
            MakeForecast(new("m2"), (Temperature, -1.0)),
            MakeForecast(new("m3"), (Temperature, -3.0)));

        var alert = AlertEvaluator.EvaluateFrost(ToConsensus(snap), [0, -5, -15]);

        Assert.Equal(AlertSeverity.Yellow, alert.Severity);
        Assert.Equal(1.0, alert.Confidence);
    }

    [Fact]
    public void Frost_no_model_agrees()
    {
        var snap = SnapshotWith(
            MakeForecast(new("m1"), (Temperature, 5.0)),
            MakeForecast(new("m2"), (Temperature, 3.0)));

        var alert = AlertEvaluator.EvaluateFrost(ToConsensus(snap), [0, -5, -15]);

        Assert.Equal(AlertSeverity.None, alert.Severity);
        Assert.Equal(0.0, alert.Confidence);
    }

    [Fact]
    public void Frost_partial_agreement()
    {
        var snap = SnapshotWith(
            MakeForecast(new("m1"), (Temperature, -3.0)),
            MakeForecast(new("m2"), (Temperature, -1.0)),
            MakeForecast(new("m3"), (Temperature, 1.0)),
            MakeForecast(new("m4"), (Temperature, 5.0)));

        var alert = AlertEvaluator.EvaluateFrost(ToConsensus(snap), [0, -5, -15]);

        Assert.Equal(AlertSeverity.Yellow, alert.Severity);
        Assert.Equal(0.5, alert.Confidence);
    }

    [Fact]
    public void Frost_moderate_produces_orange()
    {
        var snap = SnapshotWith(
            MakeForecast(new("m1"), (Temperature, -8.0)),
            MakeForecast(new("m2"), (Temperature, -7.0)));

        var alert = AlertEvaluator.EvaluateFrost(ToConsensus(snap), [0, -5, -15]);

        Assert.Equal(AlertSeverity.Orange, alert.Severity);
    }

    [Fact]
    public void Frost_severe_produces_red()
    {
        var snap = SnapshotWith(
            MakeForecast(new("m1"), (Temperature, -18.0)),
            MakeForecast(new("m2"), (Temperature, -16.0)));

        var alert = AlertEvaluator.EvaluateFrost(ToConsensus(snap), [0, -5, -15]);

        Assert.Equal(AlertSeverity.Red, alert.Severity);
    }

    // --- Heat ---

    [Fact]
    public void Heat_extreme()
    {
        var snap = SnapshotWith(
            MakeForecast(new("m1"), (ApparentTemp, 42.0)),
            MakeForecast(new("m2"), (ApparentTemp, 41.0)),
            MakeForecast(new("m3"), (ApparentTemp, 28.0)));

        var alert = AlertEvaluator.EvaluateHeat(ToConsensus(snap), [30, 35, 40]);

        Assert.Equal(AlertSeverity.Red, alert.Severity);
        Assert.True(alert.Confidence > 0.5);
    }

    [Fact]
    public void Heat_moderate()
    {
        var snap = SnapshotWith(
            MakeForecast(new("m1"), (ApparentTemp, 32.0)),
            MakeForecast(new("m2"), (ApparentTemp, 31.0)));

        var alert = AlertEvaluator.EvaluateHeat(ToConsensus(snap), [30, 35, 40]);

        Assert.Equal(AlertSeverity.Yellow, alert.Severity);
        Assert.Equal(1.0, alert.Confidence);
    }

    [Fact]
    public void Heat_below_threshold_returns_none_alert()
    {
        var snap = SnapshotWith(
            MakeForecast(new("m1"), (ApparentTemp, 28.0), (Temperature, 27.0)),
            MakeForecast(new("m2"), (ApparentTemp, 27.0), (Temperature, 26.0)));

        var alert = AlertEvaluator.EvaluateHeat(ToConsensus(snap), [30, 35, 40]);

        Assert.Equal(AlertSeverity.None, alert.Severity);
        Assert.Equal(0.0, alert.TriggerValue);
        Assert.Equal(0.0, alert.Threshold);
        Assert.Empty(alert.Attributes);
    }

    [Fact]
    public void Heat_fires_when_raw_temp_exceeds_threshold_but_apparent_does_not()
    {
        var snap = SnapshotWith(
            MakeForecast(new("m1"), (ApparentTemp, 28.0), (Temperature, 32.0)),
            MakeForecast(new("m2"), (ApparentTemp, 27.0), (Temperature, 31.0)));

        var alert = AlertEvaluator.EvaluateHeat(ToConsensus(snap), [30, 35, 40]);

        Assert.Equal(AlertSeverity.Yellow, alert.Severity);
        Assert.Equal(31.5, alert.TriggerValue);
    }

    [Fact]
    public void HeavyRain_below_threshold_preserves_trigger_value()
    {
        var snap = SnapshotWith(
            MakeForecast(new("m1"), (Precipitation, 0.5)),
            MakeForecast(new("m2"), (Precipitation, 0.3)));

        var alert = AlertEvaluator.EvaluateHeavyRain(ToConsensus(snap), 10.0, 25.0);

        Assert.Equal(AlertSeverity.None, alert.Severity);
        Assert.True(alert.TriggerValue > 0);
    }

    // --- Storm ---

    [Fact]
    public void Storm_detected()
    {
        var snap = SnapshotWith(
            MakeForecast(new("m1"), (WindGusts, 20.0)),
            MakeForecast(new("m2"), (WindGusts, 18.0)),
            MakeForecast(new("m3"), (WindGusts, 10.0)));

        var alert = AlertEvaluator.EvaluateStorm(ToConsensus(snap), [17, 25, 33]);

        Assert.Equal(AlertSeverity.Yellow, alert.Severity);
        Assert.True(alert.Confidence > 0.6);
    }

    [Fact]
    public void Storm_not_detected()
    {
        var snap = SnapshotWith(
            MakeForecast(new("m1"), (WindGusts, 10.0)),
            MakeForecast(new("m2"), (WindGusts, 8.0)));

        var alert = AlertEvaluator.EvaluateStorm(ToConsensus(snap), [17, 25, 33]);

        Assert.Equal(AlertSeverity.None, alert.Severity);
    }

    [Fact]
    public void Storm_severe_produces_orange()
    {
        var snap = SnapshotWith(
            MakeForecast(new("m1"), (WindGusts, 28.0)),
            MakeForecast(new("m2"), (WindGusts, 26.0)));

        var alert = AlertEvaluator.EvaluateStorm(ToConsensus(snap), [17, 25, 33]);

        Assert.Equal(AlertSeverity.Orange, alert.Severity);
    }

    [Fact]
    public void Storm_hurricane_produces_red()
    {
        var snap = SnapshotWith(
            MakeForecast(new("m1"), (WindGusts, 36.0)),
            MakeForecast(new("m2"), (WindGusts, 34.0)));

        var alert = AlertEvaluator.EvaluateStorm(ToConsensus(snap), [17, 25, 33]);

        Assert.Equal(AlertSeverity.Red, alert.Severity);
    }

    // --- Heavy Rain ---

    [Fact]
    public void HeavyRain_hourly_and_daily_both_exceeded()
    {
        var snap = SnapshotWith(
            MakeForecast(new("m1"), (Precipitation, 12.0)),
            MakeForecast(new("m2"), (Precipitation, 11.0)));

        var alert = AlertEvaluator.EvaluateHeavyRain(ToConsensus(snap), 10.0, 25.0);

        Assert.Equal(AlertSeverity.Red, alert.Severity);
    }

    // --- UV ---

    [Fact]
    public void Uv_high()
    {
        var snap = SnapshotWith(
            MakeForecast(new("m1"), (UvIndexParam, 7.5)),
            MakeForecast(new("m2"), (UvIndexParam, 8.0)));

        var alert = AlertEvaluator.EvaluateUv(ToConsensus(snap));

        Assert.Equal(AlertSeverity.Orange, alert.Severity);
        Assert.Equal("high", alert.Attributes["uv_level"]);
    }

    [Fact]
    public void Uv_low()
    {
        var snap = SnapshotWith(
            MakeForecast(new("m1"), (UvIndexParam, 2.0)),
            MakeForecast(new("m2"), (UvIndexParam, 1.5)));

        var alert = AlertEvaluator.EvaluateUv(ToConsensus(snap));

        Assert.Equal(AlertSeverity.None, alert.Severity);
        Assert.Equal("low", alert.Attributes["uv_level"]);
    }

    // --- Fog ---

    [Fact]
    public void Fog_likely()
    {
        var snap = SnapshotWith(
            MakeForecast(new("m1"), (Temperature, 5.0), (DewpointParam, 4.5), (WindSpeed, 1.0), (HumidityParam, 95.0)),
            MakeForecast(new("m2"), (Temperature, 5.0), (DewpointParam, 4.0), (WindSpeed, 2.0), (HumidityParam, 92.0)));

        var alert = AlertEvaluator.EvaluateFog(ToConsensus(snap), fogPersistentHours: 25);

        Assert.Equal(AlertSeverity.Yellow, alert.Severity);
        Assert.Equal(1.0, alert.Confidence);
    }

    [Fact]
    public void Fog_not_likely()
    {
        var snap = SnapshotWith(
            MakeForecast(new("m1"), (Temperature, 20.0), (DewpointParam, 10.0), (WindSpeed, 5.0), (HumidityParam, 60.0)));

        var alert = AlertEvaluator.EvaluateFog(ToConsensus(snap));

        Assert.Equal(AlertSeverity.None, alert.Severity);
    }

    [Fact]
    public void Fog_persistent_produces_orange()
    {
        var snap = SnapshotWith(
            MakeForecast(new("m1"), (Temperature, 5.0), (DewpointParam, 4.5), (WindSpeed, 1.0), (HumidityParam, 95.0)),
            MakeForecast(new("m2"), (Temperature, 5.0), (DewpointParam, 4.0), (WindSpeed, 2.0), (HumidityParam, 92.0)));

        var alert = AlertEvaluator.EvaluateFog(ToConsensus(snap), fogPersistentHours: 4);

        Assert.Equal(AlertSeverity.Orange, alert.Severity);
    }

    // --- Snow ---

    [Fact]
    public void Snow_light()
    {
        var snap = SnapshotWith(
            MakeForecast(new("m1"), (Snowfall, 0.1)),
            MakeForecast(new("m2"), (Snowfall, 0.0)));

        var alert = AlertEvaluator.EvaluateSnow(ToConsensus(snap));

        Assert.Equal(AlertSeverity.Yellow, alert.Severity);
        Assert.Equal(1.0, alert.Confidence);
    }

    // --- Pressure Drop ---

    [Fact]
    public void PressureDrop_front_approaching()
    {
        ModelForecast MakePressureForecast(WeatherModel model)
        {
            var points = new List<ForecastPoint>();
            for (var h = 0; h < 24; h++)
            {
                var pressure = 1020.0 - (h < 6 ? h * 2.5 : 0);
                points.Add(new ForecastPoint(T0.AddHours(h),
                    new Dictionary<ParameterDef, double?> { [PressureMsl] = pressure }));
            }
            return new ModelForecast(model, "lucerne", new CycleId(T0),
                new ForecastSeries(points), DailyForecastSeries.Empty);
        }

        var snap = SnapshotWith(
            MakePressureForecast(new("m1")),
            MakePressureForecast(new("m2")));

        var alert = AlertEvaluator.EvaluatePressureDrop(ToConsensus(snap), 5.0, 15.0);

        Assert.Equal(AlertSeverity.Yellow, alert.Severity);
        Assert.Equal(1.0, alert.Confidence);
    }

    [Fact]
    public void PressureDrop_stable()
    {
        var snap = SnapshotWith(
            MakeForecast(new("m1"), (PressureMsl, 1015.0)));

        var alert = AlertEvaluator.EvaluatePressureDrop(ToConsensus(snap), 5.0, 10.0);

        Assert.Equal(AlertSeverity.None, alert.Severity);
    }

    [Fact]
    public void PressureDrop_severe_produces_orange()
    {
        ModelForecast MakeSeverePressureForecast(WeatherModel model)
        {
            var points = new List<ForecastPoint>();
            for (var h = 0; h < 24; h++)
            {
                var pressure = 1020.0 - (h < 4 ? h * 4.0 : 0);
                points.Add(new ForecastPoint(T0.AddHours(h),
                    new Dictionary<ParameterDef, double?> { [PressureMsl] = pressure }));
            }
            return new ModelForecast(model, "lucerne", new CycleId(T0),
                new ForecastSeries(points), DailyForecastSeries.Empty);
        }

        var snap = SnapshotWith(
            MakeSeverePressureForecast(new("m1")),
            MakeSeverePressureForecast(new("m2")));

        var alert = AlertEvaluator.EvaluatePressureDrop(ToConsensus(snap), 5.0, 10.0);

        Assert.Equal(AlertSeverity.Orange, alert.Severity);
    }

    // --- Thunderstorm ---

    [Fact]
    public void Thunderstorm_likely()
    {
        var snap = SnapshotWith(
            MakeForecast(new("m1"), (Cape, 1500.0), (Precipitation, 10.0), (WindGusts, 20.0)),
            MakeForecast(new("m2"), (Cape, 1200.0), (Precipitation, 8.0), (WindGusts, 18.0)));

        var alert = AlertEvaluator.EvaluateThunderstorm(ToConsensus(snap), 1000, 5, 15);

        Assert.True(alert.Severity >= AlertSeverity.Orange);
        Assert.Equal(1.0, alert.Confidence);
    }

    [Fact]
    public void Thunderstorm_none()
    {
        var snap = SnapshotWith(
            MakeForecast(new("m1"), (Cape, 200.0), (Precipitation, 1.0), (WindGusts, 5.0)));

        var alert = AlertEvaluator.EvaluateThunderstorm(ToConsensus(snap), 1000, 5, 15);

        Assert.Equal(AlertSeverity.None, alert.Severity);
    }

    // --- Ice ---

    [Fact]
    public void Ice_rain_at_near_freezing_produces_yellow()
    {
        var snap = SnapshotWith(
            MakeForecast(new("m1"), (Temperature, 1.5), (RainParam, 2.0)),
            MakeForecast(new("m2"), (Temperature, 1.0), (RainParam, 1.5)));

        var alert = AlertEvaluator.EvaluateIce(ToConsensus(snap), 2.0);

        Assert.Equal(AlertSeverity.Yellow, alert.Severity);
        Assert.True(alert.Confidence > 0);
    }

    [Fact]
    public void Ice_freezing_rain_produces_orange()
    {
        var snap = SnapshotWith(
            MakeForecast(new("m1"), (Temperature, -1.0), (RainParam, 2.0)),
            MakeForecast(new("m2"), (Temperature, -0.5), (RainParam, 1.5)));

        var alert = AlertEvaluator.EvaluateIce(ToConsensus(snap), 2.0);

        Assert.Equal(AlertSeverity.Orange, alert.Severity);
    }

    [Fact]
    public void Ice_frozen_ground_produces_red()
    {
        var snap = SnapshotWith(
            MakeForecast(new("m1"), (Temperature, -1.0), (RainParam, 2.0), (SoilTemp0cm, -2.0)),
            MakeForecast(new("m2"), (Temperature, -0.5), (RainParam, 1.5), (SoilTemp0cm, -1.0)));

        var alert = AlertEvaluator.EvaluateIce(ToConsensus(snap), 2.0);

        Assert.Equal(AlertSeverity.Red, alert.Severity);
        Assert.Equal(true, alert.Attributes["soil_frozen"]);
    }

    [Fact]
    public void Ice_snow_only_no_rain_produces_none()
    {
        var snap = SnapshotWith(
            MakeForecast(new("m1"), (Temperature, -3.0), (RainParam, 0.0), (Precipitation, 5.0)),
            MakeForecast(new("m2"), (Temperature, -2.0), (RainParam, 0.0), (Precipitation, 4.0)));

        var alert = AlertEvaluator.EvaluateIce(ToConsensus(snap), 2.0);

        Assert.Equal(AlertSeverity.None, alert.Severity);
    }

    [Fact]
    public void Ice_without_soil_temp_caps_at_orange()
    {
        var snap = SnapshotWith(
            MakeForecast(new("m1"), (Temperature, -1.0), (RainParam, 2.0)),
            MakeForecast(new("m2"), (Temperature, -0.5), (RainParam, 1.5)));

        var alert = AlertEvaluator.EvaluateIce(ToConsensus(snap), 2.0);

        Assert.Equal(AlertSeverity.Orange, alert.Severity);
        Assert.Equal(false, alert.Attributes["soil_frozen"]);
    }

    // --- WindChill ---

    [Fact]
    public void WindChill_moderate_produces_yellow()
    {
        var snap = SnapshotWith(
            MakeForecast(new("m1"), (ApparentTemp, -12.0), (Temperature, -5.0)),
            MakeForecast(new("m2"), (ApparentTemp, -11.0), (Temperature, -4.0)));

        var alert = AlertEvaluator.EvaluateWindChill(ToConsensus(snap), [-10, -20, -30]);

        Assert.Equal(AlertSeverity.Yellow, alert.Severity);
        Assert.True((double)alert.Attributes["wind_factor"]! > 5.0);
    }

    [Fact]
    public void WindChill_severe_produces_orange_with_frostbite_risk()
    {
        var snap = SnapshotWith(
            MakeForecast(new("m1"), (ApparentTemp, -22.0), (Temperature, -10.0)),
            MakeForecast(new("m2"), (ApparentTemp, -21.0), (Temperature, -9.0)));

        var alert = AlertEvaluator.EvaluateWindChill(ToConsensus(snap), [-10, -20, -30]);

        Assert.Equal(AlertSeverity.Orange, alert.Severity);
        Assert.Equal("frostbite_30min", alert.Attributes["exposure_risk"]);
    }

    [Fact]
    public void WindChill_extreme_produces_red()
    {
        var snap = SnapshotWith(
            MakeForecast(new("m1"), (ApparentTemp, -35.0), (Temperature, -20.0)),
            MakeForecast(new("m2"), (ApparentTemp, -33.0), (Temperature, -18.0)));

        var alert = AlertEvaluator.EvaluateWindChill(ToConsensus(snap), [-10, -20, -30]);

        Assert.Equal(AlertSeverity.Red, alert.Severity);
        Assert.Equal("frostbite_10min", alert.Attributes["exposure_risk"]);
    }

    [Fact]
    public void WindChill_cold_but_not_extreme_produces_none()
    {
        var snap = SnapshotWith(
            MakeForecast(new("m1"), (ApparentTemp, -5.0), (Temperature, -2.0)),
            MakeForecast(new("m2"), (ApparentTemp, -4.0), (Temperature, -1.0)));

        var alert = AlertEvaluator.EvaluateWindChill(ToConsensus(snap), [-10, -20, -30]);

        Assert.Equal(AlertSeverity.None, alert.Severity);
    }

    // --- Visibility ---

    [Fact]
    public void Visibility_reduced_produces_yellow()
    {
        var snap = SnapshotWith(
            MakeForecast(new("m1"), (VisibilityParam, 800.0)),
            MakeForecast(new("m2"), (VisibilityParam, 900.0)));

        var alert = AlertEvaluator.EvaluateVisibility(ToConsensus(snap), [1000, 200, 50]);

        Assert.Equal(AlertSeverity.Yellow, alert.Severity);
    }

    [Fact]
    public void Visibility_dense_fog_produces_orange()
    {
        var snap = SnapshotWith(
            MakeForecast(new("m1"), (VisibilityParam, 150.0)),
            MakeForecast(new("m2"), (VisibilityParam, 180.0)));

        var alert = AlertEvaluator.EvaluateVisibility(ToConsensus(snap), [1000, 200, 50]);

        Assert.Equal(AlertSeverity.Orange, alert.Severity);
    }

    [Fact]
    public void Visibility_near_zero_produces_red()
    {
        var snap = SnapshotWith(
            MakeForecast(new("m1"), (VisibilityParam, 30.0)),
            MakeForecast(new("m2"), (VisibilityParam, 40.0)));

        var alert = AlertEvaluator.EvaluateVisibility(ToConsensus(snap), [1000, 200, 50]);

        Assert.Equal(AlertSeverity.Red, alert.Severity);
    }

    [Fact]
    public void Visibility_good_produces_none()
    {
        var snap = SnapshotWith(
            MakeForecast(new("m1"), (VisibilityParam, 5000.0)),
            MakeForecast(new("m2"), (VisibilityParam, 8000.0)));

        var alert = AlertEvaluator.EvaluateVisibility(ToConsensus(snap), [1000, 200, 50]);

        Assert.Equal(AlertSeverity.None, alert.Severity);
    }

    // --- TropicalNight ---

    [Fact]
    public void TropicalNight_warm_night_produces_yellow()
    {
        var snap = SnapshotWith(
            MakeForecast(new("m1"), (Temperature, 21.0), (IsDayParam, 0.0)),
            MakeForecast(new("m2"), (Temperature, 22.0), (IsDayParam, 0.0)));

        var alert = AlertEvaluator.EvaluateTropicalNight(ToConsensus(snap), [20, 23, 25]);

        Assert.Equal(AlertSeverity.Yellow, alert.Severity);
    }

    [Fact]
    public void TropicalNight_severe_produces_orange()
    {
        var snap = SnapshotWith(
            MakeForecast(new("m1"), (Temperature, 24.0), (IsDayParam, 0.0)),
            MakeForecast(new("m2"), (Temperature, 24.5), (IsDayParam, 0.0)));

        var alert = AlertEvaluator.EvaluateTropicalNight(ToConsensus(snap), [20, 23, 25]);

        Assert.Equal(AlertSeverity.Orange, alert.Severity);
    }

    [Fact]
    public void TropicalNight_extreme_produces_red()
    {
        var snap = SnapshotWith(
            MakeForecast(new("m1"), (Temperature, 26.0), (IsDayParam, 0.0)),
            MakeForecast(new("m2"), (Temperature, 27.0), (IsDayParam, 0.0)));

        var alert = AlertEvaluator.EvaluateTropicalNight(ToConsensus(snap), [20, 23, 25]);

        Assert.Equal(AlertSeverity.Red, alert.Severity);
    }

    [Fact]
    public void TropicalNight_cool_night_produces_none()
    {
        var snap = SnapshotWith(
            MakeForecast(new("m1"), (Temperature, 15.0), (IsDayParam, 0.0)),
            MakeForecast(new("m2"), (Temperature, 14.0), (IsDayParam, 0.0)));

        var alert = AlertEvaluator.EvaluateTropicalNight(ToConsensus(snap), [20, 23, 25]);

        Assert.Equal(AlertSeverity.None, alert.Severity);
    }

    [Fact]
    public void TropicalNight_all_daytime_produces_none()
    {
        var snap = SnapshotWith(
            MakeForecast(new("m1"), (Temperature, 25.0), (IsDayParam, 1.0)),
            MakeForecast(new("m2"), (Temperature, 26.0), (IsDayParam, 1.0)));

        var alert = AlertEvaluator.EvaluateTropicalNight(ToConsensus(snap), [20, 23, 25]);

        Assert.Equal(AlertSeverity.None, alert.Severity);
    }

    // --- Humidity ---

    [Fact]
    public void Humidity_muggy_produces_yellow()
    {
        var snap = SnapshotWith(
            MakeForecast(new("m1"), (DewpointParam, 18.0), (IsDayParam, 1.0)),
            MakeForecast(new("m2"), (DewpointParam, 17.0), (IsDayParam, 1.0)));

        var alert = AlertEvaluator.EvaluateHumidity(ToConsensus(snap), [16, 21, 24]);

        Assert.Equal(AlertSeverity.Yellow, alert.Severity);
        Assert.Equal("muggy", alert.Attributes["comfort_level"]);
    }

    [Fact]
    public void Humidity_oppressive_produces_orange()
    {
        var snap = SnapshotWith(
            MakeForecast(new("m1"), (DewpointParam, 22.0), (IsDayParam, 1.0)),
            MakeForecast(new("m2"), (DewpointParam, 23.0), (IsDayParam, 1.0)));

        var alert = AlertEvaluator.EvaluateHumidity(ToConsensus(snap), [16, 21, 24]);

        Assert.Equal(AlertSeverity.Orange, alert.Severity);
        Assert.Equal("oppressive", alert.Attributes["comfort_level"]);
    }

    [Fact]
    public void Humidity_tropical_produces_red()
    {
        var snap = SnapshotWith(
            MakeForecast(new("m1"), (DewpointParam, 25.0), (IsDayParam, 1.0)),
            MakeForecast(new("m2"), (DewpointParam, 26.0), (IsDayParam, 1.0)));

        var alert = AlertEvaluator.EvaluateHumidity(ToConsensus(snap), [16, 21, 24]);

        Assert.Equal(AlertSeverity.Red, alert.Severity);
        Assert.Equal("tropical", alert.Attributes["comfort_level"]);
    }

    [Fact]
    public void Humidity_comfortable_produces_none()
    {
        var snap = SnapshotWith(
            MakeForecast(new("m1"), (DewpointParam, 12.0), (IsDayParam, 1.0)),
            MakeForecast(new("m2"), (DewpointParam, 10.0), (IsDayParam, 1.0)));

        var alert = AlertEvaluator.EvaluateHumidity(ToConsensus(snap), [16, 21, 24]);

        Assert.Equal(AlertSeverity.None, alert.Severity);
    }

    [Fact]
    public void Humidity_all_nighttime_produces_none()
    {
        var snap = SnapshotWith(
            MakeForecast(new("m1"), (DewpointParam, 25.0), (IsDayParam, 0.0)),
            MakeForecast(new("m2"), (DewpointParam, 26.0), (IsDayParam, 0.0)));

        var alert = AlertEvaluator.EvaluateHumidity(ToConsensus(snap), [16, 21, 24]);

        Assert.Equal(AlertSeverity.None, alert.Severity);
    }

    // --- Daily-based alerts ---

    private static readonly ParameterDef DailyPrecipSum = ParameterRegistry.GetByApiName("precipitation_sum")!;
    private static readonly ParameterDef DailyUvMax = ParameterRegistry.GetByApiName("uv_index_max")!;
    private static readonly ParameterDef DailySnowfallSum = ParameterRegistry.GetByApiName("snowfall_sum")!;

    private static ModelForecast MakeForecastWithDaily(
        WeatherModel model,
        (ParameterDef Param, double Value)[] hourlyValues,
        (ParameterDef Param, double Value)[] dailyValues)
    {
        var today = DateOnly.FromDateTime(T0.UtcDateTime);
        var points = new List<ForecastPoint>();
        for (var h = 0; h < 24; h++)
        {
            var values = new Dictionary<ParameterDef, double?>();
            foreach (var (param, value) in hourlyValues)
                values[param] = value;
            points.Add(new ForecastPoint(T0.AddHours(h), values));
        }

        var numeric = new Dictionary<ParameterDef, double?>();
        foreach (var (param, value) in dailyValues)
            numeric[param] = value;
        var dailyPoint = new DailyForecastPoint(today, numeric, new Dictionary<ParameterDef, string?>());

        return new ModelForecast(model, "lucerne", new CycleId(T0),
            new ForecastSeries(points), new DailyForecastSeries([dailyPoint]));
    }

    [Fact]
    public void HeavyRain_daily_sum_from_daily_series_triggers_alert()
    {
        var snap = SnapshotWith(
            MakeForecastWithDaily(new("m1"),
                [(Precipitation, 1.0)],
                [(DailyPrecipSum, 35.0)]),
            MakeForecastWithDaily(new("m2"),
                [(Precipitation, 1.0)],
                [(DailyPrecipSum, 40.0)]));

        var alert = AlertEvaluator.EvaluateHeavyRain(ToConsensus(snap), 10.0, 25.0);

        Assert.True(alert.Severity >= AlertSeverity.Orange);
    }

    [Fact]
    public void Uv_daily_max_higher_than_hourly_peak()
    {
        var snap = SnapshotWith(
            MakeForecastWithDaily(new("m1"),
                [(UvIndexParam, 5.0)],
                [(DailyUvMax, 9.0)]),
            MakeForecastWithDaily(new("m2"),
                [(UvIndexParam, 4.0)],
                [(DailyUvMax, 8.5)]));

        var alert = AlertEvaluator.EvaluateUv(ToConsensus(snap));

        Assert.Equal(AlertSeverity.Red, alert.Severity);
        Assert.Equal("very_high", alert.Attributes["uv_level"]);
    }

    [Fact]
    public void Uv_daily_not_available_falls_back_to_hourly()
    {
        var snap = SnapshotWith(
            MakeForecast(new("m1"), (UvIndexParam, 7.0)),
            MakeForecast(new("m2"), (UvIndexParam, 6.5)));

        var alert = AlertEvaluator.EvaluateUv(ToConsensus(snap));

        Assert.Equal(AlertSeverity.Orange, alert.Severity);
    }

    [Fact]
    public void Snow_daily_sum_increases_severity()
    {
        var snap = SnapshotWith(
            MakeForecastWithDaily(new("m1"),
                [(Snowfall, 0.1)],
                [(DailySnowfallSum, 8.0)]),
            MakeForecastWithDaily(new("m2"),
                [(Snowfall, 0.1)],
                [(DailySnowfallSum, 7.0)]));

        var alert = AlertEvaluator.EvaluateSnow(ToConsensus(snap));

        Assert.Equal(AlertSeverity.Orange, alert.Severity);
    }

    // --- EvaluateAll ---

    [Fact]
    public void EvaluateAll_returns_14_alerts()
    {
        var snap = SnapshotWith(MakeForecast(new("m1"), (Temperature, 15.0)));
        var options = new AlertOptions();

        var result = AlertEvaluator.EvaluateAll(ToConsensus(snap), options, Time);

        Assert.Equal(14, result.Alerts.Count);
    }
}
