using Microsoft.Extensions.Time.Testing;
using Njord.Configuration;
using Njord.Domain.Weather;
using Njord.Domain.Analysis;

namespace Njord.Tests.Domain.Analysis;

public sealed class IndexResultSpec
{
    private static readonly DateTimeOffset T0 = new(2026, 7, 11, 12, 0, 0, TimeSpan.Zero);
    private static readonly FakeTimeProvider Time = new(T0);
    private static readonly ParameterDef Temperature = ParameterRegistry.GetByApiName("temperature_2m")!;
    private static readonly ParameterDef Humidity = ParameterRegistry.GetByApiName("relative_humidity_2m")!;
    private static readonly ParameterDef WindSpeed = ParameterRegistry.GetByApiName("wind_speed_10m")!;
    private static readonly ParameterDef CloudCover = ParameterRegistry.GetByApiName("cloud_cover")!;

    private static readonly ResolvedParameterSet Parameters = ParameterRegistry.Resolve(
        ["Weather", "Solar"], [], []);

    private static ModelForecast MakeForecast(
        WeatherModel model, params (ParameterDef Param, double Value)[] hourlyValues)
    {
        var points = new List<ForecastPoint>();
        for (var h = 0; h < 48; h++)
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
    public void Compute_produces_all_index_values()
    {
        var snap = SnapshotWith(
            MakeForecast(new("m1"),
                (Temperature, 22.0), (Humidity, 50.0), (WindSpeed, 3.0), (CloudCover, 20.0)));

        var result = IndexResult.Compute(snap, "lucerne", Parameters, Time, new IndexOptions());

        Assert.Equal("lucerne", result.Location);
        Assert.InRange(result.Outdoor, 1, 100);
        Assert.InRange(result.Laundry, 1, 100);
        Assert.InRange(result.Solar, 1, 100);
    }

    [Fact(Timeout = 5000)]
    public void Compute_with_multiple_models_produces_envelope()
    {
        var snap = SnapshotWith(
            MakeForecast(new("m1"), (Temperature, 22.0), (Humidity, 50.0), (WindSpeed, 3.0), (CloudCover, 20.0)),
            MakeForecast(new("m2"), (Temperature, 10.0), (Humidity, 90.0), (WindSpeed, 15.0), (CloudCover, 95.0)),
            MakeForecast(new("m3"), (Temperature, 20.0), (Humidity, 55.0), (WindSpeed, 4.0), (CloudCover, 30.0)));

        var result = IndexResult.Compute(snap, "lucerne", Parameters, Time, new IndexOptions());

        Assert.NotNull(result.OutdoorEnvelope);
        Assert.True(result.OutdoorEnvelope!.Min <= result.OutdoorEnvelope.Max);
        Assert.InRange(result.OutdoorEnvelope.Confidence, 0.0, 1.0);
    }

    [Fact(Timeout = 5000)]
    public void Compute_single_model_has_no_envelope()
    {
        var snap = SnapshotWith(
            MakeForecast(new("m1"), (Temperature, 22.0), (Humidity, 50.0), (WindSpeed, 3.0), (CloudCover, 20.0)));

        var result = IndexResult.Compute(snap, "lucerne", Parameters, Time, new IndexOptions());

        Assert.Null(result.OutdoorEnvelope);
        Assert.Null(result.LaundryEnvelope);
    }

    [Fact(Timeout = 5000)]
    public void Compute_two_agreeing_models_have_high_confidence()
    {
        var snap = SnapshotWith(
            MakeForecast(new("m1"), (Temperature, 22.0), (Humidity, 50.0), (WindSpeed, 3.0), (CloudCover, 20.0)),
            MakeForecast(new("m2"), (Temperature, 21.0), (Humidity, 52.0), (WindSpeed, 3.5), (CloudCover, 22.0)));

        var result = IndexResult.Compute(snap, "lucerne", Parameters, Time, new IndexOptions());

        Assert.NotNull(result.OutdoorEnvelope);
        Assert.Equal(1.0, result.OutdoorEnvelope!.Confidence);
        Assert.True(result.OutdoorEnvelope.Max - result.OutdoorEnvelope.Min < 20);
    }

    [Fact(Timeout = 5000)]
    public void BuildEnvelope_computes_min_max_confidence()
    {
        var envelope = IndexResult.BuildEnvelope([70, 72, 71, 73, 70]);

        Assert.Equal(70, envelope.Min);
        Assert.Equal(73, envelope.Max);
        Assert.Equal(1.0, envelope.Confidence);
    }

    [Fact(Timeout = 5000)]
    public void BuildEnvelope_low_confidence_for_wide_spread()
    {
        var envelope = IndexResult.BuildEnvelope([10, 50, 90]);

        Assert.Equal(10, envelope.Min);
        Assert.Equal(90, envelope.Max);
        Assert.True(envelope.Confidence < 1.0);
    }
}
