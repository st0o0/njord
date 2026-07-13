using System.Text.Json.Nodes;
using Microsoft.Extensions.Time.Testing;
using Njord.Domain;
using Njord.Enrichment;

namespace Njord.Tests.Enrichment;

public sealed class DerivedResultSpec
{
    private static readonly DateTimeOffset T0 = new(2026, 7, 11, 12, 0, 0, TimeSpan.Zero);
    private static readonly FakeTimeProvider Time = new(T0);
    private static readonly ParameterDef Temperature = ParameterRegistry.GetByApiName("temperature_2m")!;
    private static readonly ParameterDef WindSpeed = ParameterRegistry.GetByApiName("wind_speed_10m")!;
    private static readonly ParameterDef DewPoint = ParameterRegistry.GetByApiName("dew_point_2m")!;
    private static readonly ParameterDef WeatherCode = ParameterRegistry.GetByApiName("weather_code")!;
    private static readonly ParameterDef PressureMsl = ParameterRegistry.GetByApiName("pressure_msl")!;
    private static readonly ParameterDef SurfacePressure = ParameterRegistry.GetByApiName("surface_pressure")!;
    private static readonly ParameterDef SunshineDuration = ParameterRegistry.GetByApiName("sunshine_duration")!;
    private static readonly ParameterDef IsDay = ParameterRegistry.GetByApiName("is_day")!;

    private static readonly ResolvedParameterSet Parameters = ParameterRegistry.Resolve(
        ["Weather", "Solar"], [], []);

    private static ModelForecast MakeForecast(
        WeatherModel model, params (ParameterDef Param, double Value)[] hourlyValues)
    {
        var points = new List<ForecastPoint>();
        for (var h = 0; h < 72; h++)
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

    [Fact(Timeout = 5000)]
    public void Compute_produces_horizon_and_scalar_results()
    {
        var snap = SnapshotWith(
            MakeForecast(new("m1"),
                (Temperature, 5.0), (WindSpeed, 8.0), (DewPoint, 3.0),
                (WeatherCode, 3.0), (PressureMsl, 1020.0), (SurfacePressure, 1015.0),
                (SunshineDuration, 3600.0), (IsDay, 1.0)),
            MakeForecast(new("m2"),
                (Temperature, 7.0), (WindSpeed, 10.0), (DewPoint, 4.0),
                (WeatherCode, 3.0), (PressureMsl, 1021.0), (SurfacePressure, 1016.0),
                (SunshineDuration, 3600.0), (IsDay, 1.0)));

        var result = DerivedResult.Compute(snap, "lucerne", [3, 6], Parameters, Time);

        Assert.Equal("lucerne", result.Location);
        Assert.Equal(2, result.ByHorizon.Count);
        Assert.Contains("h3", result.ByHorizon.Keys);
        Assert.Contains("h6", result.ByHorizon.Keys);

        var h3 = result.ByHorizon["h3"];
        Assert.NotNull(h3.Beaufort);
        Assert.Equal("Overcast", h3.WmoDescription);
        Assert.Equal("dry", h3.DewPointComfort);
    }

    [Fact(Timeout = 5000)]
    public void ToMqttMessages_produces_horizon_and_meta_messages()
    {
        var snap = SnapshotWith(
            MakeForecast(new("m1"),
                (Temperature, 5.0), (WindSpeed, 8.0), (DewPoint, 3.0),
                (WeatherCode, 0.0), (PressureMsl, 1020.0), (SurfacePressure, 1015.0),
                (SunshineDuration, 3600.0), (IsDay, 1.0)));

        var result = DerivedResult.Compute(snap, "lucerne", [3], Parameters, Time);
        var messages = result.ToMqttMessages("njord");

        Assert.Equal(2, messages.Count);

        var horizonMsg = messages.First(m => m.Topic.Contains("/h3"));
        Assert.Equal("njord/lucerne/derived/h3", horizonMsg.Topic);
        Assert.True(horizonMsg.Retain);

        var json = JsonNode.Parse(horizonMsg.Payload)!;
        Assert.NotNull(json["beaufort"]);
        Assert.NotNull(json["wmo_description"]);

        var metaMsg = messages.First(m => m.Topic.Contains("/meta"));
        Assert.Equal("njord/lucerne/derived/meta", metaMsg.Topic);

        var metaJson = JsonNode.Parse(metaMsg.Payload)!;
        Assert.True(metaJson["diurnal_amplitude"] is not null || metaJson["diurnal_amplitude"]?.GetValue<object>() is null);
    }

    [Fact(Timeout = 5000)]
    public void ToMqttMessages_null_wind_chill_serializes_as_json_null()
    {
        var snap = SnapshotWith(
            MakeForecast(new("m1"),
                (Temperature, 20.0), (WindSpeed, 2.0), (DewPoint, 12.0),
                (WeatherCode, 1.0)));

        var result = DerivedResult.Compute(snap, "lucerne", [3], Parameters, Time);
        var messages = result.ToMqttMessages("njord");

        var horizonMsg = messages.First(m => m.Topic.Contains("/h3"));
        var json = JsonNode.Parse(horizonMsg.Payload)!;
        Assert.True(json["wind_chill"] is null || json["wind_chill"]!.GetValueKind() == System.Text.Json.JsonValueKind.Null);
    }
}
