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
    private static readonly ResolvedParameterSet Parameters = ParameterRegistry.Resolve(["Weather", "Solar"], [], []);

    private static ConsensusSnapshot ToConsensus(ModelSnapshot snap) =>
        ConsensusSnapshot.Compute(snap, Parameters, "lucerne", Time);

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

        var alert = AlertEvaluator.EvaluateFrost(ToConsensus(snap), 0.0);

        Assert.Equal(AlertSeverity.Yellow, alert.Severity);
        Assert.Equal(1.0, alert.Confidence);
    }

    [Fact(Timeout = 5000)]
    public void Frost_no_model_agrees()
    {
        var snap = SnapshotWith(
            MakeForecast(new("m1"), (Temperature, 5.0)),
            MakeForecast(new("m2"), (Temperature, 3.0)));

        var alert = AlertEvaluator.EvaluateFrost(ToConsensus(snap), 0.0);

        Assert.Equal(AlertSeverity.None, alert.Severity);
        Assert.Equal(0.0, alert.Confidence);
    }

    [Fact(Timeout = 5000)]
    public void Frost_partial_agreement()
    {
        // Sorted: [-3, -1, 1, 5] → median = 0.0 ≤ threshold
        // Agreement within tolerance 2.0 of median 0.0: -1 (yes), 1 (yes), -3 (no), 5 (no) → 2/4 = 0.5
        var snap = SnapshotWith(
            MakeForecast(new("m1"), (Temperature, -3.0)),
            MakeForecast(new("m2"), (Temperature, -1.0)),
            MakeForecast(new("m3"), (Temperature, 1.0)),
            MakeForecast(new("m4"), (Temperature, 5.0)));

        var alert = AlertEvaluator.EvaluateFrost(ToConsensus(snap), 0.0);

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

        var alert = AlertEvaluator.EvaluateHeat(ToConsensus(snap), [30, 35, 40]);

        Assert.Equal(AlertSeverity.Red, alert.Severity);
        Assert.True(alert.Confidence > 0.5);
    }

    [Fact(Timeout = 5000)]
    public void Heat_moderate()
    {
        var snap = SnapshotWith(
            MakeForecast(new("m1"), (ApparentTemp, 32.0)),
            MakeForecast(new("m2"), (ApparentTemp, 31.0)));

        var alert = AlertEvaluator.EvaluateHeat(ToConsensus(snap), [30, 35, 40]);

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

        var alert = AlertEvaluator.EvaluateStorm(ToConsensus(snap), 16.7);

        Assert.Equal(AlertSeverity.Yellow, alert.Severity);
        Assert.True(alert.Confidence > 0.6);
    }

    [Fact(Timeout = 5000)]
    public void Storm_not_detected()
    {
        var snap = SnapshotWith(
            MakeForecast(new("m1"), (WindGusts, 10.0)),
            MakeForecast(new("m2"), (WindGusts, 8.0)));

        var alert = AlertEvaluator.EvaluateStorm(ToConsensus(snap), 16.7);

        Assert.Equal(AlertSeverity.None, alert.Severity);
    }

    // --- Heavy Rain ---

    [Fact(Timeout = 5000)]
    public void HeavyRain_hourly_and_daily_both_exceeded()
    {
        // Median of [12, 11] = 11.5 mm/h → exceeds hourly threshold 10.0
        // Daily sum from hourly: 11.5 × 25h = 287.5 mm → exceeds daily threshold 25.0
        // Both hourly and daily thresholds crossed → Red
        var snap = SnapshotWith(
            MakeForecast(new("m1"), (Precipitation, 12.0)),
            MakeForecast(new("m2"), (Precipitation, 11.0)));

        var alert = AlertEvaluator.EvaluateHeavyRain(ToConsensus(snap), 10.0, 25.0);

        Assert.Equal(AlertSeverity.Red, alert.Severity);
    }

    // --- UV ---

    [Fact(Timeout = 5000)]
    public void Uv_high()
    {
        var snap = SnapshotWith(
            MakeForecast(new("m1"), (UvIndexParam, 7.5)),
            MakeForecast(new("m2"), (UvIndexParam, 8.0)));

        var alert = AlertEvaluator.EvaluateUv(ToConsensus(snap));

        Assert.Equal(AlertSeverity.Orange, alert.Severity);
        Assert.Equal("high", alert.Attributes["uv_level"]);
    }

    [Fact(Timeout = 5000)]
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

    [Fact(Timeout = 5000)]
    public void Fog_likely()
    {
        var snap = SnapshotWith(
            MakeForecast(new("m1"), (Temperature, 5.0), (Dewpoint, 4.5), (WindSpeed, 1.0), (Humidity, 95.0)),
            MakeForecast(new("m2"), (Temperature, 5.0), (Dewpoint, 4.0), (WindSpeed, 2.0), (Humidity, 92.0)));

        var alert = AlertEvaluator.EvaluateFog(ToConsensus(snap));

        Assert.Equal(AlertSeverity.Yellow, alert.Severity);
        Assert.Equal(1.0, alert.Confidence);
    }

    [Fact(Timeout = 5000)]
    public void Fog_not_likely()
    {
        var snap = SnapshotWith(
            MakeForecast(new("m1"), (Temperature, 20.0), (Dewpoint, 10.0), (WindSpeed, 5.0), (Humidity, 60.0)));

        var alert = AlertEvaluator.EvaluateFog(ToConsensus(snap));

        Assert.Equal(AlertSeverity.None, alert.Severity);
    }

    // --- Snow ---

    [Fact(Timeout = 5000)]
    public void Snow_light()
    {
        // Median of [0.1, 0.0] = 0.05 > 0 → snow detected
        // Both values within tolerance 2.0 of median → agreement = 1.0
        var snap = SnapshotWith(
            MakeForecast(new("m1"), (Snowfall, 0.1)),
            MakeForecast(new("m2"), (Snowfall, 0.0)));

        var alert = AlertEvaluator.EvaluateSnow(ToConsensus(snap));

        Assert.Equal(AlertSeverity.Yellow, alert.Severity);
        Assert.Equal(1.0, alert.Confidence);
    }

    // --- Pressure Drop ---

    [Fact(Timeout = 5000)]
    public void PressureDrop_front_approaching()
    {
        // Two models with same pressure drop pattern (consensus needs >= 2 models)
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

        var alert = AlertEvaluator.EvaluatePressureDrop(ToConsensus(snap), 5.0);

        Assert.Equal(AlertSeverity.Yellow, alert.Severity);
        Assert.Equal(1.0, alert.Confidence);
    }

    [Fact(Timeout = 5000)]
    public void PressureDrop_stable()
    {
        var snap = SnapshotWith(
            MakeForecast(new("m1"), (PressureMsl, 1015.0)));

        var alert = AlertEvaluator.EvaluatePressureDrop(ToConsensus(snap), 5.0);

        Assert.Equal(AlertSeverity.None, alert.Severity);
    }

    // --- Thunderstorm ---

    [Fact(Timeout = 5000)]
    public void Thunderstorm_likely()
    {
        var snap = SnapshotWith(
            MakeForecast(new("m1"), (Cape, 1500.0), (Precipitation, 10.0), (WindGusts, 20.0)),
            MakeForecast(new("m2"), (Cape, 1200.0), (Precipitation, 8.0), (WindGusts, 18.0)));

        var alert = AlertEvaluator.EvaluateThunderstorm(ToConsensus(snap), 1000, 5, 15);

        Assert.True(alert.Severity >= AlertSeverity.Orange);
        Assert.Equal(1.0, alert.Confidence);
    }

    [Fact(Timeout = 5000)]
    public void Thunderstorm_none()
    {
        var snap = SnapshotWith(
            MakeForecast(new("m1"), (Cape, 200.0), (Precipitation, 1.0), (WindGusts, 5.0)));

        var alert = AlertEvaluator.EvaluateThunderstorm(ToConsensus(snap), 1000, 5, 15);

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

    [Fact(Timeout = 5000)]
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

    [Fact(Timeout = 5000)]
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

    [Fact(Timeout = 5000)]
    public void Uv_daily_not_available_falls_back_to_hourly()
    {
        var snap = SnapshotWith(
            MakeForecast(new("m1"), (UvIndexParam, 7.0)),
            MakeForecast(new("m2"), (UvIndexParam, 6.5)));

        var alert = AlertEvaluator.EvaluateUv(ToConsensus(snap));

        Assert.Equal(AlertSeverity.Orange, alert.Severity);
    }

    [Fact(Timeout = 5000)]
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

    [Fact(Timeout = 5000)]
    public void EvaluateAll_returns_9_alerts()
    {
        var snap = SnapshotWith(MakeForecast(new("m1"), (Temperature, 15.0)));
        var options = new AlertThresholdOptions();

        var result = AlertEvaluator.EvaluateAll(ToConsensus(snap), options, Time);

        Assert.Equal(9, result.Alerts.Count);
    }
}
