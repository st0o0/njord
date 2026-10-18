using Microsoft.Extensions.Time.Testing;
using Njord.Consensus;
using Njord.Consensus.Settings;
using Njord.Core.Configuration;
using Njord.Messages.Trends;
using Njord.Model;
using Njord.Forecast;
using WeatherForecast = Njord.Forecast.Forecast;

namespace Njord.Trends.Tests;

public sealed class TrendResultSpec
{
    private static readonly DateTimeOffset T0 = new(2026, 7, 11, 12, 0, 0, TimeSpan.Zero);
    private static readonly FakeTimeProvider Time = new(T0);
    private static readonly ParameterDef Temperature = ParameterRegistry.GetByApiName("temperature_2m")!;
    private static readonly ParameterDef WindSpeed = ParameterRegistry.GetByApiName("wind_speed_10m")!;
    private static readonly ParameterDef Precipitation = ParameterRegistry.GetByApiName("precipitation")!;
    private static readonly ParameterDef CloudCover = ParameterRegistry.GetByApiName("cloud_cover")!;
    private static readonly ParameterDef WeatherCode = ParameterRegistry.GetByApiName("weather_code")!;

    private static readonly ResolvedParameterSet Parameters = ParameterSelection.Resolve(
        ["Weather"], [], []);

    private static WeatherForecast MakeForecast(
        WeatherModel model, params (ParameterDef Param, double Value)[] hourlyValues)
    {
        var points = new List<ForecastPoint>();
        for (var h = 0; h < 72; h++)
        {
            var values = new Dictionary<ParameterDef, ForecastValue>();
            foreach (var (param, value) in hourlyValues)
            {
                values[param] = value;
            }

            points.Add(new ForecastPoint(T0.AddHours(h), values));
        }
        return new WeatherForecast(model, "lucerne", new CycleId(T0),
            new ForecastSeries(points), DailyForecastSeries.Empty);
    }

    private static ModelForecasts ForecastsWith(params WeatherForecast[] forecasts)
    {
        var snap = ModelForecasts.Empty;
        foreach (var f in forecasts)
        {
            snap = snap.Update(f);
        }

        return snap;
    }

    [Fact]
    public void Compute_with_no_previous_produces_null_trends()
    {
        var snap = ForecastsWith(
            MakeForecast(new WeatherModel("m1"), (Temperature, 20.0), (WindSpeed, 5.0),
                (Precipitation, 0.0), (CloudCover, 50.0), (WeatherCode, 3.0)));

        var consensus = new ConsensusBuilder(Parameters, Time).Create(snap, "lucerne", ConsensusSettings.Default);
        var result = TrendAnalyzer.Analyze(consensus, null);

        Assert.Equal(new LocationId("lucerne"), result.Location);
        Assert.All(result.ParameterTrends.Values, v => Assert.Null(v));
        Assert.Null(result.WeatherChange);
        Assert.Null(result.Stability);
    }

    [Fact]
    public void Compute_with_previous_detects_rising_temperature()
    {
        var prev = ForecastsWith(
            MakeForecast(new WeatherModel("m1"), (Temperature, 18.0), (WindSpeed, 5.0),
                (Precipitation, 0.0), (CloudCover, 50.0), (WeatherCode, 1.0)),
            MakeForecast(new WeatherModel("m2"), (Temperature, 18.0), (WindSpeed, 5.0),
                (Precipitation, 0.0), (CloudCover, 50.0), (WeatherCode, 1.0)));
        var curr = ForecastsWith(
            MakeForecast(new WeatherModel("m1"), (Temperature, 22.0), (WindSpeed, 5.0),
                (Precipitation, 0.0), (CloudCover, 50.0), (WeatherCode, 1.0)),
            MakeForecast(new WeatherModel("m2"), (Temperature, 22.0), (WindSpeed, 5.0),
                (Precipitation, 0.0), (CloudCover, 50.0), (WeatherCode, 1.0)));

        var prevConsensus = new ConsensusBuilder(Parameters, Time).Create(prev, "lucerne", ConsensusSettings.Default);
        var currConsensus = new ConsensusBuilder(Parameters, Time).Create(curr, "lucerne", ConsensusSettings.Default);
        var result = TrendAnalyzer.Analyze(currConsensus, prevConsensus);

        var tempTrend = result.ParameterTrends["temperature_2m"];
        Assert.NotNull(tempTrend);
        Assert.Equal(TrendDirection.Rising, tempTrend.Direction);
        Assert.Equal(4.0, tempTrend.Delta);
    }

    [Fact]
    public void Compute_detects_weather_change()
    {
        var prev = ForecastsWith(
            MakeForecast(new WeatherModel("m1"), (Temperature, 20.0), (WeatherCode, 1.0)),
            MakeForecast(new WeatherModel("m2"), (Temperature, 20.0), (WeatherCode, 1.0)));
        var curr = ForecastsWith(
            MakeForecast(new WeatherModel("m1"), (Temperature, 20.0), (WeatherCode, 63.0)),
            MakeForecast(new WeatherModel("m2"), (Temperature, 20.0), (WeatherCode, 63.0)));

        var prevConsensus = new ConsensusBuilder(Parameters, Time).Create(prev, "lucerne", ConsensusSettings.Default);
        var currConsensus = new ConsensusBuilder(Parameters, Time).Create(curr, "lucerne", ConsensusSettings.Default);
        var result = TrendAnalyzer.Analyze(currConsensus, prevConsensus);

        Assert.NotNull(result.WeatherChange);
        Assert.Equal(WeatherCategory.Clear, result.WeatherChange.From);
        Assert.Equal(WeatherCategory.Rain, result.WeatherChange.To);
    }

}
