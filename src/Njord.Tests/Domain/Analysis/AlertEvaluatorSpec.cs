using Microsoft.Extensions.Time.Testing;
using Njord.Configuration;
using Njord.Domain.Weather;
using Njord.Domain.Analysis;

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
    private static readonly ParameterDef Dewpoint = ParameterRegistry.GetByApiName("dew_point_2m")!;
    private static readonly ParameterDef WindSpeed = ParameterRegistry.GetByApiName("wind_speed_10m")!;
    private static readonly ParameterDef Humidity = ParameterRegistry.GetByApiName("relative_humidity_2m")!;
    private static readonly ParameterDef Snowfall = ParameterRegistry.GetByApiName("snowfall")!;
    private static readonly ParameterDef PressureMsl = ParameterRegistry.GetByApiName("pressure_msl")!;
    private static readonly ParameterDef Cape = ParameterRegistry.GetByApiName("cape")!;

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

    [Fact(Timeout = 5000)]
    public void Frost_all_models_agree()
    {
        var snap = SnapshotWith(
            MakeForecast(new("m1"), (Temperature, -2.0)),
            MakeForecast(new("m2"), (Temperature, -1.0)),
            MakeForecast(new("m3"), (Temperature, -3.0)));

        var alert = AlertEvaluator.EvaluateFrost(snap, "lucerne", 0.0, Time);

        Assert.Equal(AlertSeverity.Yellow, alert.Severity);
        Assert.Equal(1.0, alert.Confidence);
    }

    [Fact(Timeout = 5000)]
    public void Frost_no_model_agrees()
    {
        var snap = SnapshotWith(
            MakeForecast(new("m1"), (Temperature, 5.0)),
            MakeForecast(new("m2"), (Temperature, 3.0)));

        var alert = AlertEvaluator.EvaluateFrost(snap, "lucerne", 0.0, Time);

        Assert.Equal(AlertSeverity.None, alert.Severity);
        Assert.Equal(0.0, alert.Confidence);
    }

    [Fact(Timeout = 5000)]
    public void Frost_partial_agreement()
    {
        var snap = SnapshotWith(
            MakeForecast(new("m1"), (Temperature, -1.0)),
            MakeForecast(new("m2"), (Temperature, 2.0)),
            MakeForecast(new("m3"), (Temperature, 5.0)),
            MakeForecast(new("m4"), (Temperature, -0.5)));

        var alert = AlertEvaluator.EvaluateFrost(snap, "lucerne", 0.0, Time);

        Assert.Equal(AlertSeverity.Yellow, alert.Severity);
        Assert.Equal(0.5, alert.Confidence);
    }

    // --- Heat ---

    [Fact(Timeout = 5000)]
    public void Heat_extreme()
    {
        var snap = SnapshotWith(
            MakeForecast(new("m1"), (ApparentTemp, 42.0)),
            MakeForecast(new("m2"), (ApparentTemp, 41.0)),
            MakeForecast(new("m3"), (ApparentTemp, 28.0)));

        var alert = AlertEvaluator.EvaluateHeat(snap, "lucerne", [30, 35, 40], Time);

        Assert.Equal(AlertSeverity.Red, alert.Severity);
        Assert.True(alert.Confidence > 0.5);
    }

    [Fact(Timeout = 5000)]
    public void Heat_moderate()
    {
        var snap = SnapshotWith(
            MakeForecast(new("m1"), (ApparentTemp, 32.0)),
            MakeForecast(new("m2"), (ApparentTemp, 31.0)));

        var alert = AlertEvaluator.EvaluateHeat(snap, "lucerne", [30, 35, 40], Time);

        Assert.Equal(AlertSeverity.Yellow, alert.Severity);
        Assert.Equal(1.0, alert.Confidence);
    }

    // --- Storm ---

    [Fact(Timeout = 5000)]
    public void Storm_detected()
    {
        var snap = SnapshotWith(
            MakeForecast(new("m1"), (WindGusts, 20.0)),
            MakeForecast(new("m2"), (WindGusts, 18.0)),
            MakeForecast(new("m3"), (WindGusts, 10.0)));

        var alert = AlertEvaluator.EvaluateStorm(snap, "lucerne", 16.7, Time);

        Assert.Equal(AlertSeverity.Yellow, alert.Severity);
        Assert.True(alert.Confidence > 0.6);
    }

    [Fact(Timeout = 5000)]
    public void Storm_not_detected()
    {
        var snap = SnapshotWith(
            MakeForecast(new("m1"), (WindGusts, 10.0)),
            MakeForecast(new("m2"), (WindGusts, 8.0)));

        var alert = AlertEvaluator.EvaluateStorm(snap, "lucerne", 16.7, Time);

        Assert.Equal(AlertSeverity.None, alert.Severity);
    }

    // --- Heavy Rain ---

    [Fact(Timeout = 5000)]
    public void HeavyRain_hourly_and_daily_both_exceeded()
    {
        // 12mm/h × 24h = 288mm daily → both hourly and daily thresholds crossed → Red
        var snap = SnapshotWith(
            MakeForecast(new("m1"), (Precipitation, 12.0)),
            MakeForecast(new("m2"), (Precipitation, 5.0)));

        var alert = AlertEvaluator.EvaluateHeavyRain(snap, "lucerne", 10.0, 25.0, Time);

        Assert.Equal(AlertSeverity.Red, alert.Severity);
    }

    // --- UV ---

    [Fact(Timeout = 5000)]
    public void Uv_high()
    {
        var snap = SnapshotWith(
            MakeForecast(new("m1"), (UvIndexParam, 7.5)),
            MakeForecast(new("m2"), (UvIndexParam, 8.0)));

        var alert = AlertEvaluator.EvaluateUv(snap, "lucerne", Time);

        Assert.Equal(AlertSeverity.Orange, alert.Severity);
        Assert.Equal("high", alert.Attributes["uv_level"]);
    }

    [Fact(Timeout = 5000)]
    public void Uv_low()
    {
        var snap = SnapshotWith(
            MakeForecast(new("m1"), (UvIndexParam, 2.0)),
            MakeForecast(new("m2"), (UvIndexParam, 1.5)));

        var alert = AlertEvaluator.EvaluateUv(snap, "lucerne", Time);

        Assert.Equal(AlertSeverity.None, alert.Severity);
        Assert.Equal("low", alert.Attributes["uv_level"]);
    }

    // --- Fog ---

    [Fact(Timeout = 5000)]
    public void Fog_likely()
    {
        var snap = SnapshotWith(
            MakeForecast(new("m1"), (Temperature, 5.0), (Dewpoint, 4.5), (WindSpeed, 1.0), (Humidity, 95.0)),
            MakeForecast(new("m2"), (Temperature, 5.0), (Dewpoint, 4.0), (WindSpeed, 2.0), (Humidity, 92.0)));

        var alert = AlertEvaluator.EvaluateFog(snap, "lucerne", Time);

        Assert.Equal(AlertSeverity.Yellow, alert.Severity);
        Assert.Equal(1.0, alert.Confidence);
    }

    [Fact(Timeout = 5000)]
    public void Fog_not_likely()
    {
        var snap = SnapshotWith(
            MakeForecast(new("m1"), (Temperature, 20.0), (Dewpoint, 10.0), (WindSpeed, 5.0), (Humidity, 60.0)));

        var alert = AlertEvaluator.EvaluateFog(snap, "lucerne", Time);

        Assert.Equal(AlertSeverity.None, alert.Severity);
    }

    // --- Snow ---

    [Fact(Timeout = 5000)]
    public void Snow_light()
    {
        var snap = SnapshotWith(
            MakeForecast(new("m1"), (Snowfall, 0.1)),
            MakeForecast(new("m2"), (Snowfall, 0.0)));

        var alert = AlertEvaluator.EvaluateSnow(snap, "lucerne", Time);

        Assert.Equal(AlertSeverity.Yellow, alert.Severity);
        Assert.Equal(0.5, alert.Confidence);
    }

    // --- Pressure Drop ---

    [Fact(Timeout = 5000)]
    public void PressureDrop_front_approaching()
    {
        var points = new List<ForecastPoint>();
        for (var h = 0; h < 24; h++)
        {
            var pressure = 1020.0 - (h < 6 ? h * 2.5 : 0);
            points.Add(new ForecastPoint(T0.AddHours(h),
                new Dictionary<ParameterDef, double?> { [PressureMsl] = pressure }));
        }
        var forecast = new ModelForecast(new("m1"), "lucerne", new CycleId(T0),
            new ForecastSeries(points), DailyForecastSeries.Empty);
        var snap = ModelSnapshot.Empty.Update(forecast);

        var alert = AlertEvaluator.EvaluatePressureDrop(snap, "lucerne", 5.0, Time);

        Assert.Equal(AlertSeverity.Yellow, alert.Severity);
        Assert.Equal(1.0, alert.Confidence);
    }

    [Fact(Timeout = 5000)]
    public void PressureDrop_stable()
    {
        var snap = SnapshotWith(
            MakeForecast(new("m1"), (PressureMsl, 1015.0)));

        var alert = AlertEvaluator.EvaluatePressureDrop(snap, "lucerne", 5.0, Time);

        Assert.Equal(AlertSeverity.None, alert.Severity);
    }

    // --- Thunderstorm ---

    [Fact(Timeout = 5000)]
    public void Thunderstorm_likely()
    {
        var snap = SnapshotWith(
            MakeForecast(new("m1"), (Cape, 1500.0), (Precipitation, 10.0), (WindGusts, 20.0)),
            MakeForecast(new("m2"), (Cape, 1200.0), (Precipitation, 8.0), (WindGusts, 18.0)));

        var alert = AlertEvaluator.EvaluateThunderstorm(snap, "lucerne", 1000, 5, 15, Time);

        Assert.True(alert.Severity >= AlertSeverity.Orange);
        Assert.Equal(1.0, alert.Confidence);
    }

    [Fact(Timeout = 5000)]
    public void Thunderstorm_none()
    {
        var snap = SnapshotWith(
            MakeForecast(new("m1"), (Cape, 200.0), (Precipitation, 1.0), (WindGusts, 5.0)));

        var alert = AlertEvaluator.EvaluateThunderstorm(snap, "lucerne", 1000, 5, 15, Time);

        Assert.Equal(AlertSeverity.None, alert.Severity);
    }

    // --- EvaluateAll ---

    [Fact(Timeout = 5000)]
    public void EvaluateAll_returns_9_alerts()
    {
        var snap = SnapshotWith(MakeForecast(new("m1"), (Temperature, 15.0)));
        var options = new AlertThresholdOptions();

        var result = AlertEvaluator.EvaluateAll(snap, "lucerne", options, Time);

        Assert.Equal(9, result.Alerts.Count);
    }
}
