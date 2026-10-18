using System.Text.Json.Nodes;
using Microsoft.Extensions.Time.Testing;
using Njord.Compute.Consensus;
using Njord.Compute.Contracts;
using Njord.Compute.Settings;
using Njord.Core.Configuration;
using Njord.Core.Configuration.Indices;
using Njord.Derived;
using Njord.Indices;
using Njord.Tests.Shared;
using Njord.Trends;
using Njord.Weather;

namespace Njord.Mqtt.Tests;

public sealed class StatePayloadBuilderSpec
{
    private static IndexSettings DefaultPrefs(string location = "lucerne") =>
        AppSettingsDefaults.Options<IndexOptions>().ToSettings([location]);
    private static readonly WeatherModel IconD2 = new("icon_d2");
    private static readonly ParameterDef Temperature = ParameterRegistry.GetByApiName("temperature_2m")!;
    private static readonly ParameterDef WindSpeed = ParameterRegistry.GetByApiName("wind_speed_10m")!;
    private static readonly ParameterDef TempMax = ParameterRegistry.GetByApiName("temperature_2m_max")!;
    private static readonly ParameterDef Sunrise = ParameterRegistry.GetByApiName("sunrise")!;

    private static readonly ResolvedParameterSet SmallParams = new(
        [Temperature, WindSpeed],
        [TempMax, Sunrise]);

    private static Forecast Forecast(DateTimeOffset tick, DateTimeOffset firstPoint, int pointCount)
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

    [Fact]
    public void Returns_one_entry_per_configured_horizon()
    {
        var tick = TestTimeConstants.NoonAnchor;
        var result = StatePayloadBuilder.BuildPerHorizon(Forecast(tick, tick, 100), SmallParams, [3, 6, 24], 4, tick);

        Assert.True(result.ContainsKey(Horizon.Hours(3)));
        Assert.True(result.ContainsKey(Horizon.Hours(6)));
        Assert.True(result.ContainsKey(Horizon.Hours(24)));
        Assert.True(result.ContainsKey(Horizon.Days(0)));
    }

    [Fact]
    public void Hourly_payload_is_flat_json_with_parameter_values()
    {
        var tick = TestTimeConstants.NoonAnchor;
        var result = StatePayloadBuilder.BuildPerHorizon(Forecast(tick, tick, 100), SmallParams, [3], 1, tick);
        var h3 = JsonNode.Parse(result[Horizon.Hours(3)])!;

        Assert.Equal(3.0, (double?)h3["temperature"]);
        Assert.InRange((double)h3["wind_speed_10m"]!, 0.29, 0.31);
        Assert.False(h3.AsObject().ContainsKey("h3"));
    }

    [Fact]
    public void Daily_payload_is_flat_json_with_parameter_values()
    {
        var tick = TestTimeConstants.NoonAnchor;
        var result = StatePayloadBuilder.BuildPerHorizon(Forecast(tick, tick, 10), SmallParams, [3], 1, tick);
        var d0 = JsonNode.Parse(result[Horizon.Days(0)])!;

        Assert.Equal(28.5, (double?)d0["temperature_max"]);
        Assert.Equal("05:31", (string?)d0["sunrise"]);
    }

    [Fact]
    public void Horizons_beyond_series_are_omitted()
    {
        var tick = TestTimeConstants.NoonAnchor;
        var result = StatePayloadBuilder.BuildPerHorizon(Forecast(tick, tick, 10), SmallParams, [72], 1, tick);

        Assert.False(result.ContainsKey(Horizon.Hours(72)));
    }

    [Fact]
    public void Anchor_floors_to_current_hour_when_tick_is_mid_hour()
    {
        var tick = new DateTimeOffset(2026, 7, 12, 19, 31, 0, TimeSpan.Zero);
        var firstPoint = new DateTimeOffset(2026, 7, 12, 20, 0, 0, TimeSpan.Zero);
        var result = StatePayloadBuilder.BuildPerHorizon(Forecast(tick, firstPoint, 100), SmallParams, [3], 1, tick);
        var h3 = JsonNode.Parse(result[Horizon.Hours(3)])!;

        Assert.Equal(2.0, (double?)h3["temperature"]);
    }

    private static readonly DateTimeOffset T0 = TestTimeConstants.ForecastEpoch;
    private static readonly FakeTimeProvider Time = TestTimeouts.AtEpoch(T0);
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
    private static readonly ResolvedParameterSet FullParams = ParameterSelection.Resolve(["Weather", "Solar"], [], []);

    private static Forecast MakeForecast(WeatherModel model, params (ParameterDef Param, double Value)[] hourlyValues)
    {
        var values = hourlyValues.ToDictionary(x => x.Param, x => (double?)x.Value);
        return new Forecast(
            model, "lucerne", new CycleId(T0),
            new ForecastSeries(Enumerable.Range(0, 72)
                .Select(i => new ForecastPoint(T0.AddHours(i), values))),
            new DailyForecastSeries([]));
    }

    private static ModelSnapshot SnapshotWith(params Forecast[] forecasts)
        => forecasts.Aggregate(ModelSnapshot.Empty, (s, f) => s.Update(f));

    [Fact]
    public void FromConsensus_produces_one_message_per_horizon()
    {
        var snap = SnapshotWith(
            MakeForecast(IconD2, (Temperature, 20.0)),
            MakeForecast(new WeatherModel("ecmwf_ifs025"), (Temperature, 22.0)));
        var consensus = new ConsensusBuilder(new ResolvedParameterSet([Temperature], []), Time).Create(snap, "lucerne", ConsensusSettings.Default);
        var result = new ConsensusResult(consensus.Hourly.Parameters, consensus.Daily.Parameters);

        var messages = StatePayloadBuilder.FromConsensus(result, "njord", "lucerne");

        Assert.True(messages.Count > 0);
        Assert.All(messages, m => Assert.StartsWith("njord/lucerne/consensus/h", m.Topic));
        Assert.All(messages, m => Assert.True(m.Retain));
        var h3 = messages.FirstOrDefault(m => m.Topic == "njord/lucerne/consensus/h3");
        Assert.NotNull(h3);
        var payload = JsonNode.Parse(h3.Payload)!;
        Assert.NotNull(payload["temperature"]);
        Assert.Equal(2, payload["temperature_models"]!.GetValue<int>());
        Assert.Null(payload["_models_used"]);
    }

    [Fact]
    public void FromConsensus_produces_daily_messages_for_daily_parameters()
    {
        var baseDate = DateOnly.FromDateTime(T0.UtcDateTime);
        var dailyParam = ParameterRegistry.GetByApiName("temperature_2m_max")!;
        var dailyForecasts = new[]
        {
            new Forecast(IconD2, "lucerne", new CycleId(T0), new ForecastSeries([]),
                new DailyForecastSeries([
                    new DailyForecastPoint(baseDate, new Dictionary<ParameterDef, double?> { [dailyParam] = 28.0 }, new Dictionary<ParameterDef, string?>()),
                    new DailyForecastPoint(baseDate.AddDays(1), new Dictionary<ParameterDef, double?> { [dailyParam] = 30.0 }, new Dictionary<ParameterDef, string?>()),
                ])),
            new Forecast(new WeatherModel("ecmwf_ifs025"), "lucerne", new CycleId(T0), new ForecastSeries([]),
                new DailyForecastSeries([
                    new DailyForecastPoint(baseDate, new Dictionary<ParameterDef, double?> { [dailyParam] = 31.0 }, new Dictionary<ParameterDef, string?>()),
                    new DailyForecastPoint(baseDate.AddDays(1), new Dictionary<ParameterDef, double?> { [dailyParam] = 33.0 }, new Dictionary<ParameterDef, string?>()),
                ])),
        };

        var snap = dailyForecasts.Aggregate(ModelSnapshot.Empty, (s, f) => s.Update(f));
        var consensus = new ConsensusBuilder(new ResolvedParameterSet([], [dailyParam]), Time).Create(snap, "lucerne", ConsensusSettings.Default);
        var result = new ConsensusResult(consensus.Hourly.Parameters, consensus.Daily.Parameters);

        var messages = StatePayloadBuilder.FromConsensus(result, "njord", "lucerne");

        Assert.Equal(2, messages.Count);
        Assert.Contains(messages, m => m.Topic == "njord/lucerne/consensus/d0");
        Assert.Contains(messages, m => m.Topic == "njord/lucerne/consensus/d1");

        var d0 = JsonNode.Parse(messages.First(m => m.Topic.Contains("/d0")).Payload)!;
        Assert.NotNull(d0["temperature_max"]);
        Assert.Equal(2, d0["temperature_max_models"]!.GetValue<int>());
    }

    [Fact]
    public void FromAlerts_produces_one_message_per_alert()
    {
        var alerts = new List<Alert>
        {
            new(AlertType.Frost, AlertSeverity.Yellow, 0.75, new FrostDetails(-2.1, Horizon.Hours(4), 3)),
            Alert.None(AlertType.Heat),
        };
        var result = new AlertResult(new LocationId("lucerne"), alerts);

        var messages = StatePayloadBuilder.FromAlerts(result, "njord");

        Assert.Equal(2, messages.Count);
        Assert.Equal("njord/lucerne/alerts/frost", messages[0].Topic);
        Assert.Equal("njord/lucerne/alerts/heat", messages[1].Topic);
        Assert.True(messages[0].Retain);
        var payload = JsonNode.Parse(messages[0].Payload)!;
        Assert.Equal("yellow", (string?)payload["severity"]);
        Assert.Equal(0.75, (double?)payload["confidence"]);
    }

    [Fact]
    public void FromAlerts_writes_frost_details_as_snake_case_attributes()
    {
        var alert = new Alert(AlertType.Frost, AlertSeverity.Yellow, 0.75, new FrostDetails(-2.1, Horizon.Hours(4), 3));

        var message = Assert.Single(StatePayloadBuilder.FromAlerts(
            new AlertResult(new LocationId("lucerne"), [alert]), "njord"));

        Assert.Equal(
            "{\"severity\":\"yellow\",\"confidence\":0.75,\"trigger_value\":0,\"threshold\":0,"
            + "\"peak_value\":null,\"hours_until\":null,\"duration_hours\":null,"
            + "\"expected_low\":-2.1,\"earliest_frost\":\"\\u002B4h\",\"models_agreeing\":3}",
            message.Payload);
    }

    [Fact]
    public void FromDerived_produces_horizon_and_meta_messages()
    {
        var snap = SnapshotWith(
            MakeForecast(M1,
                (Temperature, 5.0), (WindSpeed, 8.0), (DewPoint, 3.0),
                (WeatherCode, 0.0), (PressureMsl, 1020.0), (SurfacePressure, 1015.0),
                (SunshineDuration, 3600.0), (IsDay, 1.0)),
            MakeForecast(new WeatherModel("m2"),
                (Temperature, 5.0), (WindSpeed, 8.0), (DewPoint, 3.0),
                (WeatherCode, 0.0), (PressureMsl, 1020.0), (SurfacePressure, 1015.0),
                (SunshineDuration, 3600.0), (IsDay, 1.0)));
        var consensus = new ConsensusBuilder(FullParams, Time).Create(snap, "lucerne", ConsensusSettings.Default);
        var result = DerivedAnalyzer.Analyze(consensus, new DerivedSettings([3]));

        var messages = StatePayloadBuilder.FromDerived(result, "njord");

        Assert.Equal(2, messages.Count);
        var horizonMsg = messages.First(m => m.Topic.Contains("/h3"));
        Assert.Equal("njord/lucerne/derived/h3", horizonMsg.Topic);
        var json = JsonNode.Parse(horizonMsg.Payload)!;
        Assert.NotNull(json["beaufort"]);
    }

    [Fact]
    public void FromTrends_produces_single_trend_topic()
    {
        var snap = SnapshotWith(
            MakeForecast(M1, (Temperature, 20.0), (WindSpeed, 5.0),
                (Precipitation, 0.0), (CloudCover, 50.0), (WeatherCode, 1.0)));
        var consensus = new ConsensusBuilder(FullParams, Time).Create(snap, "lucerne", ConsensusSettings.Default);
        var result = TrendAnalyzer.Analyze(consensus, null);

        var messages = StatePayloadBuilder.FromTrends(result, "njord");

        var message = Assert.Single(messages);
        Assert.Equal("njord/lucerne/trends", message.Topic);
        Assert.True(message.Retain);
    }

    [Fact]
    public void FromIndices_produces_per_day_topics()
    {
        var snap = SnapshotWith(
            MakeForecast(M1, (Temperature, 20.0), (Humidity, 55.0), (WindSpeed, 2.0), (CloudCover, 30.0)),
            MakeForecast(new WeatherModel("m2"), (Temperature, 21.0), (Humidity, 56.0), (WindSpeed, 2.5), (CloudCover, 32.0)));
        var consensus = new ConsensusBuilder(FullParams, Time).Create(snap, "lucerne", ConsensusSettings.Default);
        var result = FeatureServices.Indices(Time).Compute(consensus, null, DefaultPrefs());
        Assert.NotNull(result);

        var messages = StatePayloadBuilder.FromIndices(result, "njord");

        Assert.True(messages.Count >= 1);
        Assert.Contains(messages, m => m.Topic == "njord/lucerne/indices/d0");
        Assert.All(messages, m => Assert.True(m.Retain));
    }

    [Fact]
    public void FromIndices_includes_envelope_fields_when_multiple_models()
    {
        var snap = SnapshotWith(
            MakeForecast(IconD2, (Temperature, 22.0), (Humidity, 50.0), (WindSpeed, 3.0), (CloudCover, 20.0)),
            MakeForecast(new WeatherModel("ecmwf_ifs025"), (Temperature, 10.0), (Humidity, 90.0), (WindSpeed, 15.0), (CloudCover, 95.0)));
        var consensus = new ConsensusBuilder(FullParams, Time).Create(snap, "lucerne", ConsensusSettings.Default);
        var result = FeatureServices.Indices(Time).Compute(consensus, null, DefaultPrefs());
        Assert.NotNull(result);

        var messages = StatePayloadBuilder.FromIndices(result, "njord");
        var d0Message = messages.First(m => m.Topic.Contains("/d0"));
        var payload = JsonNode.Parse(d0Message.Payload)!;

        Assert.NotNull(payload["outdoor_min"]);
        Assert.NotNull(payload["outdoor_max"]);
        Assert.NotNull(payload["outdoor_confidence"]);
        Assert.True(payload["outdoor_min"]!.GetValue<int>() <= payload["outdoor_max"]!.GetValue<int>());
    }

}
