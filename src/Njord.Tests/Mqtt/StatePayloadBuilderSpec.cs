using System.Text.Json.Nodes;
using Microsoft.Extensions.Time.Testing;
using Njord.Configuration;
using Njord.Domain.Analysis;
using Njord.Domain.Weather;
using Njord.Egress;
using Njord.Mqtt;

namespace Njord.Tests.Mqtt;

public sealed class StatePayloadBuilderSpec
{
    private static readonly WeatherModel IconD2 = new("icon_d2");
    private static readonly ParameterDef Temperature = ParameterRegistry.GetByApiName("temperature_2m")!;
    private static readonly ParameterDef WindSpeed = ParameterRegistry.GetByApiName("wind_speed_10m")!;
    private static readonly ParameterDef TempMax = ParameterRegistry.GetByApiName("temperature_2m_max")!;
    private static readonly ParameterDef Sunrise = ParameterRegistry.GetByApiName("sunrise")!;

    private static readonly ResolvedParameterSet SmallParams = new(
        [Temperature, WindSpeed],
        [TempMax, Sunrise]);

    private static ModelForecast Forecast(DateTimeOffset tick, DateTimeOffset firstPoint, int pointCount)
        => new(
            IconD2,
            "home",
            new CycleId(tick),
            new ForecastSeries(Enumerable.Range(0, pointCount)
                .Select(i => new ForecastPoint(firstPoint.AddHours(i), new Dictionary<ParameterDef, double?>
                {
                    [Temperature] = i,
                    [WindSpeed] = i * 0.1,
                }))),
            new DailyForecastSeries([
                new DailyForecastPoint(DateOnly.FromDateTime(tick.UtcDateTime),
                    new Dictionary<ParameterDef, double?> { [TempMax] = 28.5 },
                    new Dictionary<ParameterDef, string?> { [Sunrise] = "05:31" }),
            ]));

    [Fact(Timeout = 5000)]
    public void Returns_one_entry_per_configured_horizon()
    {
        var tick = new DateTimeOffset(2026, 7, 12, 12, 0, 0, TimeSpan.Zero);
        var result = StatePayloadBuilder.BuildPerHorizon(Forecast(tick, tick, 100), SmallParams, [3, 6, 24], 4, tick);

        Assert.Equal(7, result.Count);
        Assert.True(result.ContainsKey("h3"));
        Assert.True(result.ContainsKey("h6"));
        Assert.True(result.ContainsKey("h24"));
        Assert.True(result.ContainsKey("d0"));
        Assert.True(result.ContainsKey("d1"));
        Assert.True(result.ContainsKey("d2"));
        Assert.True(result.ContainsKey("d3"));
    }

    [Fact(Timeout = 5000)]
    public void Hourly_payload_is_flat_json_with_parameter_values()
    {
        var tick = new DateTimeOffset(2026, 7, 12, 12, 0, 0, TimeSpan.Zero);
        var result = StatePayloadBuilder.BuildPerHorizon(Forecast(tick, tick, 100), SmallParams, [3], 1, tick);
        var h3 = JsonNode.Parse(result["h3"])!;

        Assert.Equal(3.0, (double?)h3["temperature"]);
        Assert.InRange((double)h3["wind_speed_10m"]!, 0.29, 0.31);
        Assert.False(h3.AsObject().ContainsKey("h3"));
    }

    [Fact(Timeout = 5000)]
    public void Daily_payload_is_flat_json_with_parameter_values()
    {
        var tick = new DateTimeOffset(2026, 7, 12, 12, 0, 0, TimeSpan.Zero);
        var result = StatePayloadBuilder.BuildPerHorizon(Forecast(tick, tick, 10), SmallParams, [3], 1, tick);
        var d0 = JsonNode.Parse(result["d0"])!;

        Assert.Equal(28.5, (double?)d0["temperature_max"]);
        Assert.Equal("05:31", (string?)d0["sunrise"]);
    }

    [Fact(Timeout = 5000)]
    public void Horizons_beyond_series_yield_null_values()
    {
        var tick = new DateTimeOffset(2026, 7, 12, 12, 0, 0, TimeSpan.Zero);
        var result = StatePayloadBuilder.BuildPerHorizon(Forecast(tick, tick, 10), SmallParams, [72], 1, tick);
        var h72 = JsonNode.Parse(result["h72"])!;

        Assert.True(h72.AsObject().ContainsKey("temperature"));
        Assert.Null(h72["temperature"]);
    }

    [Fact(Timeout = 5000)]
    public void Anchor_bumps_to_next_full_hour_when_tick_is_mid_hour()
    {
        var tick = new DateTimeOffset(2026, 7, 12, 19, 31, 0, TimeSpan.Zero);
        var firstPoint = new DateTimeOffset(2026, 7, 12, 20, 0, 0, TimeSpan.Zero);
        var result = StatePayloadBuilder.BuildPerHorizon(Forecast(tick, firstPoint, 100), SmallParams, [3], 1, tick);
        var h3 = JsonNode.Parse(result["h3"])!;

        Assert.Equal(3.0, (double?)h3["temperature"]);
    }

    private static readonly DateTimeOffset T0 = new(2026, 7, 11, 12, 0, 0, TimeSpan.Zero);
    private static readonly FakeTimeProvider Time = new(T0);
    private static readonly WeatherModel M1 = new("m1");
    private static readonly ParameterDef DewPoint = ParameterRegistry.GetByApiName("dew_point_2m")!;
    private static readonly ParameterDef WeatherCode = ParameterRegistry.GetByApiName("weather_code")!;
    private static readonly ParameterDef CloudCover = ParameterRegistry.GetByApiName("cloud_cover")!;
    private static readonly ParameterDef Humidity = ParameterRegistry.GetByApiName("relative_humidity_2m")!;
    private static readonly ParameterDef Precipitation = ParameterRegistry.GetByApiName("precipitation")!;
    private static readonly ParameterDef PressureMsl = ParameterRegistry.GetByApiName("pressure_msl")!;
    private static readonly ParameterDef SurfacePressure = ParameterRegistry.GetByApiName("surface_pressure")!;
    private static readonly ParameterDef SunshineDuration = ParameterRegistry.GetByApiName("sunshine_duration")!;
    private static readonly ParameterDef IsDay = ParameterRegistry.GetByApiName("is_day")!;
    private static readonly ResolvedParameterSet FullParams = ParameterRegistry.Resolve(["Weather", "Solar"], [], []);

    private static ModelForecast MakeForecast(WeatherModel model, params (ParameterDef Param, double Value)[] hourlyValues)
    {
        var values = hourlyValues.ToDictionary(x => x.Param, x => (double?)x.Value);
        return new ModelForecast(
            model, "lucerne", new CycleId(T0),
            new ForecastSeries(Enumerable.Range(0, 72)
                .Select(i => new ForecastPoint(T0.AddHours(i), values))),
            new DailyForecastSeries([]));
    }

    private static ModelSnapshot SnapshotWith(params ModelForecast[] forecasts)
        => forecasts.Aggregate(ModelSnapshot.Empty, (s, f) => s.Update(f));

    [Fact(Timeout = 5000)]
    public void FromConsensus_produces_one_message_per_horizon()
    {
        var snap = SnapshotWith(
            MakeForecast(IconD2, (Temperature, 20.0)),
            MakeForecast(new("ecmwf_ifs025"), (Temperature, 22.0)));
        var result = ConsensusResult.Compute(snap, new ResolvedParameterSet([Temperature], []), [3], "lucerne", Time);

        var messages = StatePayloadBuilder.FromConsensus(result, "njord", "lucerne");

        Assert.Single(messages);
        Assert.Equal("njord/lucerne/consensus/h3", messages[0].Topic);
        Assert.True(messages[0].Retain);
        var payload = JsonNode.Parse(messages[0].Payload)!;
        Assert.NotNull(payload["temperature"]);
        Assert.NotNull(payload["_models_used"]);
    }

    [Fact(Timeout = 5000)]
    public void FromAlerts_produces_one_message_per_alert()
    {
        var alerts = new List<Alert>
        {
            new(AlertType.Frost, AlertSeverity.Yellow, 0.75, new Dictionary<string, object?> { ["expected_low"] = -2.1 }),
            Alert.None(AlertType.Heat),
        };
        var result = new AlertResult("lucerne", alerts);

        var messages = StatePayloadBuilder.FromAlerts(result, "njord");

        Assert.Equal(2, messages.Count);
        Assert.Equal("njord/lucerne/alerts/frost", messages[0].Topic);
        Assert.Equal("njord/lucerne/alerts/heat", messages[1].Topic);
        Assert.True(messages[0].Retain);
        var payload = JsonNode.Parse(messages[0].Payload)!;
        Assert.Equal("yellow", (string?)payload["severity"]);
        Assert.Equal(0.75, (double?)payload["confidence"]);
    }

    [Fact(Timeout = 5000)]
    public void FromDerived_produces_horizon_and_meta_messages()
    {
        var snap = SnapshotWith(
            MakeForecast(M1,
                (Temperature, 5.0), (WindSpeed, 8.0), (DewPoint, 3.0),
                (WeatherCode, 0.0), (PressureMsl, 1020.0), (SurfacePressure, 1015.0),
                (SunshineDuration, 3600.0), (IsDay, 1.0)));
        var result = DerivedResult.Compute(snap, "lucerne", [3], FullParams, Time);

        var messages = StatePayloadBuilder.FromDerived(result, "njord");

        Assert.Equal(2, messages.Count);
        var horizonMsg = messages.First(m => m.Topic.Contains("/h3"));
        Assert.Equal("njord/lucerne/derived/h3", horizonMsg.Topic);
        var json = JsonNode.Parse(horizonMsg.Payload)!;
        Assert.NotNull(json["beaufort"]);
    }

    [Fact(Timeout = 5000)]
    public void FromTrends_produces_single_trend_topic()
    {
        var snap = SnapshotWith(
            MakeForecast(M1, (Temperature, 20.0), (WindSpeed, 5.0),
                (Precipitation, 0.0), (CloudCover, 50.0), (WeatherCode, 1.0)));
        var result = TrendResult.Compute(snap, null, "lucerne", [3], FullParams, Time);

        var messages = StatePayloadBuilder.FromTrends(result, "njord");

        Assert.Single(messages);
        Assert.Equal("njord/lucerne/trends", messages[0].Topic);
        Assert.True(messages[0].Retain);
    }

    [Fact(Timeout = 5000)]
    public void FromIndices_produces_single_index_topic()
    {
        var snap = SnapshotWith(
            MakeForecast(M1, (Temperature, 20.0), (Humidity, 55.0), (WindSpeed, 2.0), (CloudCover, 30.0)));
        var result = IndexResult.Compute(snap, "lucerne", FullParams, Time, new IndexOptions());

        var messages = StatePayloadBuilder.FromIndices(result, "njord");

        Assert.Single(messages);
        Assert.Equal("njord/lucerne/indices", messages[0].Topic);
        Assert.True(messages[0].Retain);
    }

    [Fact(Timeout = 5000)]
    public void FromEnergy_produces_single_energy_topic()
    {
        var snap = SnapshotWith(
            MakeForecast(M1, (Temperature, 15.0), (WindSpeed, 2.0), (CloudCover, 30.0)));
        var result = EnergyResult.Compute(snap, "lucerne", FullParams, Time, new EnergyOptions());

        var messages = StatePayloadBuilder.FromEnergy(result, "njord");

        Assert.Single(messages);
        Assert.Equal("njord/lucerne/energy", messages[0].Topic);
        Assert.True(messages[0].Retain);
    }

    [Fact(Timeout = 5000)]
    public void FromHistory_produces_single_history_topic()
    {
        var history = new ForecastHistory(30);
        var result = HistoryResult.Compute(history, ModelSnapshot.Empty, "lucerne", FullParams, Time, new HistoryOptions());

        var messages = StatePayloadBuilder.FromHistory(result, "njord");

        Assert.Single(messages);
        Assert.Equal("njord/lucerne/history", messages[0].Topic);
        Assert.True(messages[0].Retain);
    }
}
