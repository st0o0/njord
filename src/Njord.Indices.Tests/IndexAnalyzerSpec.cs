using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;
using Njord.Compute.Analysis;
using Njord.Core.Configuration;
using Njord.Core.Configuration.Indices;
using Njord.Sensors;
using Njord.Weather;

namespace Njord.Indices.Tests;

public sealed class IndexAnalyzerSpec
{
    private static readonly DateTimeOffset T0 = new(2026, 7, 14, 6, 0, 0, TimeSpan.Zero);
    private static readonly FakeTimeProvider Time = new(T0);
    private static readonly ResolvedParameterSet Parameters = ParameterRegistry.Resolve(["Weather"], [], []);
    private static readonly ParameterDef Temperature = ParameterRegistry.GetByApiName("temperature_2m")!;
    private static readonly ParameterDef Humidity = ParameterRegistry.GetByApiName("relative_humidity_2m")!;
    private static readonly ParameterDef WindSpeed = ParameterRegistry.GetByApiName("wind_speed_10m")!;
    private static readonly ParameterDef CloudCover = ParameterRegistry.GetByApiName("cloud_cover")!;
    private static readonly ParameterDef IsDay = ParameterRegistry.IsDay;

    [Fact]
    public void Compute_returns_index_result()
    {
        var analyzer = CreateAnalyzer();

        var result = analyzer.Compute(BuildConsensus(), null);

        Assert.NotNull(result);
        Assert.Equal(new LocationId("lucerne"), result.Location);
    }

    [Fact]
    public void Compute_returns_null_when_disabled()
    {
        var analyzer = CreateAnalyzer(enabled: false);

        var result = analyzer.Compute(BuildConsensus(), null);

        Assert.Null(result);
    }

    [Fact]
    public void Compute_with_sensor_overrides_indoor_temp()
    {
        var analyzer = CreateAnalyzer(indoorTemp: 28.0);
        var consensus = BuildConsensusWithWeather("lucerne");
        var sensors = BuildSensorSnapshot("lucerne", 18.0);

        var withSensor = analyzer.Compute(consensus, sensors);
        var withoutSensor = analyzer.Compute(consensus, null);

        Assert.NotNull(withSensor);
        Assert.NotNull(withoutSensor);
        Assert.NotEmpty(withSensor.Days);
        Assert.NotEmpty(withoutSensor.Days);
        Assert.NotEqual(withoutSensor.Days[0].NightVentilation, withSensor.Days[0].NightVentilation);
    }

    [Fact]
    public void Sensor_override_does_not_persist_across_calls()
    {
        var analyzer = CreateAnalyzer(indoorTemp: 28.0);
        var consensus = BuildConsensusWithWeather("lucerne");
        var sensors = BuildSensorSnapshot("lucerne", 18.0);

        _ = analyzer.Compute(consensus, sensors);
        var afterSensorCall = analyzer.Compute(consensus, null);
        var fresh = analyzer.Compute(consensus, null);

        Assert.NotNull(afterSensorCall);
        Assert.NotNull(fresh);
        Assert.NotEmpty(afterSensorCall.Days);
        Assert.NotEmpty(fresh.Days);
        Assert.Equal(fresh.Days[0].NightVentilation, afterSensorCall.Days[0].NightVentilation);
    }

    private static IndexAnalyzer CreateAnalyzer(
        bool enabled = true,
        double? indoorTemp = null)
    {
        var indexComputer = new IndexComputer(Parameters, Time);
        var indexOptions = new IndexOptions { Enabled = enabled };
        if (indoorTemp.HasValue)
        {
            indexOptions.Preferences.IndoorTemp = indoorTemp.Value;
        }

        var njordOptions = new NjordOptions { Locations = [new LocationOptions { Name = "lucerne" }] };
        return new IndexAnalyzer(
            indexComputer,
            Options.Create(indexOptions),
            Options.Create(njordOptions));
    }

    private static ConsensusSnapshot BuildConsensus(string location = "lucerne")
    {
        var snap = ModelSnapshot.Empty;
        return new ConsensusSnapshotFactory(Parameters, Time).Create(snap, location);
    }

    private static ConsensusSnapshot BuildConsensusWithWeather(string location)
    {
        var snap = ModelSnapshot.Empty
            .Update(MakeForecast(new WeatherModel("m1"), location, 24, _ => (15.0, 60.0, 3.0, 30.0, 0.0)))
            .Update(MakeForecast(new WeatherModel("m2"), location, 24, _ => (15.0, 60.0, 3.0, 30.0, 0.0)));
        return new ConsensusSnapshotFactory(Parameters, Time).Create(snap, location);
    }

    private static Forecast MakeForecast(
        WeatherModel model, string location, int hours,
        Func<int, (double temp, double hum, double wind, double cloud, double isDay)> valueFunc)
    {
        var points = new List<ForecastPoint>();
        for (var h = 0; h < hours; h++)
        {
            var (temp, hum, wind, cloud, isD) = valueFunc(h);
            var values = new Dictionary<ParameterDef, double?>
            {
                [Temperature] = temp,
                [Humidity] = hum,
                [WindSpeed] = wind,
                [CloudCover] = cloud,
                [IsDay] = isD,
            };
            points.Add(new ForecastPoint(T0.AddHours(h), values));
        }
        return new Forecast(model, location, new CycleId(T0),
            new ForecastSeries(points), DailyForecastSeries.Empty);
    }

    private static SensorSnapshot BuildSensorSnapshot(string location, double indoorTemp)
    {
        var readings = new Dictionary<SensorKind, AggregatedReading>
        {
            [SensorKind.IndoorTemperature] = new(indoorTemp, 1, T0),
        };
        return new SensorSnapshot(location, readings);
    }
}
