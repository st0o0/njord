using Microsoft.Extensions.Options;
using Njord.Configuration;
using Njord.Domain.Analysis;
using Njord.Domain.Sensors;
using Njord.Domain.Weather;
using Njord.Egress;
using Njord.Enrichment.Features;

namespace Njord.Tests.Enrichment.Features;

public sealed class IndexEnrichmentSpec
{
    private static readonly DateTimeOffset T0 = new(2026, 7, 14, 12, 0, 0, TimeSpan.Zero);
    private static readonly ResolvedParameterSet Parameters = ParameterRegistry.Resolve(["Weather"], [], []);

    private sealed class FakeTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }

    private static IndexEnrichment CreateFeature()
    {
        var enrichment = new EnrichmentOptions();
        var njordOptions = new NjordOptions { Locations = [new() { Name = "lucerne" }] };
        var parameters = ParameterRegistry.Resolve(["Weather"], [], []);

        return new IndexEnrichment(Options.Create(enrichment), Options.Create(njordOptions), parameters, new FakeTimeProvider(T0));
    }

    private static ModelForecast BuildForecast(string location)
    {
        var temp = ParameterRegistry.GetByApiName("temperature_2m")!;
        var wind = ParameterRegistry.GetByApiName("wind_speed_10m")!;
        var humidity = ParameterRegistry.GetByApiName("relative_humidity_2m")!;
        var points = new List<ForecastPoint>();
        for (var h = 0; h < 48; h++)
            points.Add(new ForecastPoint(T0.AddHours(h),
                new Dictionary<ParameterDef, double?>
                {
                    [temp] = 25.0,
                    [wind] = 4.0,
                    [humidity] = 55.0,
                }));

        return new ModelForecast(new WeatherModel("icon_d2"), location, new CycleId(T0),
            new ForecastSeries(points), DailyForecastSeries.Empty);
    }

    [Fact(Timeout = 5000)]
    public void Snapshot_with_temperature_data_produces_index_result()
    {
        var feature = CreateFeature();
        var snapshot = ModelSnapshot.Empty.Update(BuildForecast("lucerne"));

        var consensus = ConsensusSnapshot.Compute(snapshot, Parameters, "lucerne", new FakeTimeProvider(T0));
        var events = feature.Compute(consensus).ToList();

        var update = Assert.Single(events);
        var enrichment = Assert.IsType<EgressEvent.EnrichmentUpdate>(update);
        Assert.Equal("lucerne", enrichment.Location);
        Assert.Equal("indices", enrichment.TypeName);
        Assert.NotNull(enrichment.Result);
    }

    [Fact(Timeout = 5000)]
    public void Empty_snapshot_produces_event_with_empty_result()
    {
        var feature = CreateFeature();

        var consensus = ConsensusSnapshot.Compute(ModelSnapshot.Empty, Parameters, "lucerne", new FakeTimeProvider(T0));
        var events = feature.Compute(consensus).ToList();

        var update = Assert.Single(events);
        var enrichment = Assert.IsType<EgressEvent.EnrichmentUpdate>(update);
        Assert.Equal("indices", enrichment.TypeName);
    }

    private static ModelForecast BuildForecast(string location, string modelId, double temp)
    {
        var tempParam = ParameterRegistry.GetByApiName("temperature_2m")!;
        var wind = ParameterRegistry.GetByApiName("wind_speed_10m")!;
        var humidity = ParameterRegistry.GetByApiName("relative_humidity_2m")!;
        var points = new List<ForecastPoint>();
        for (var h = 0; h < 48; h++)
            points.Add(new ForecastPoint(T0.AddHours(h),
                new Dictionary<ParameterDef, double?>
                {
                    [tempParam] = temp,
                    [wind] = 4.0,
                    [humidity] = 55.0,
                }));

        return new ModelForecast(new WeatherModel(modelId), location, new CycleId(T0),
            new ForecastSeries(points), DailyForecastSeries.Empty);
    }

    private static ConsensusSnapshot BuildTwoModelConsensus(string location, double temp = 25.0)
    {
        var snapshot = ModelSnapshot.Empty
            .Update(BuildForecast(location, "icon_d2", temp))
            .Update(BuildForecast(location, "icon_eu", temp));
        return ConsensusSnapshot.Compute(snapshot, Parameters, location, new FakeTimeProvider(T0));
    }

    [Fact(Timeout = 5000)]
    public void Sensor_indoor_temperature_overrides_config_value()
    {
        var enrichment = new EnrichmentOptions();
        enrichment.Indices.Preferences.IndoorTemp = 18.0;
        var njordOptions = new NjordOptions { Locations = [new() { Name = "lucerne" }] };
        var feature = new IndexEnrichment(
            Options.Create(enrichment), Options.Create(njordOptions), Parameters, new FakeTimeProvider(T0));

        var consensus = BuildTwoModelConsensus("lucerne");

        var sensorSnapshot = new SensorSnapshot("lucerne", new Dictionary<SensorKind, AggregatedReading>
        {
            [SensorKind.IndoorTemperature] = new(30.0, 1, T0),
        });

        var withSensor = feature.Compute(consensus, sensorSnapshot).ToList();
        var withoutSensor = feature.Compute(consensus).ToList();

        var resultWith = Assert.IsType<IndexResult>(Assert.IsType<EgressEvent.EnrichmentUpdate>(Assert.Single(withSensor)).Result);
        var resultWithout = Assert.IsType<IndexResult>(Assert.IsType<EgressEvent.EnrichmentUpdate>(Assert.Single(withoutSensor)).Result);

        // Ventilation score depends on IndoorTemp; sensor value (30) vs config (18) should differ
        Assert.NotEqual(resultWith.Ventilation, resultWithout.Ventilation);
    }

    [Fact(Timeout = 5000)]
    public void Null_sensor_snapshot_falls_back_to_config_value()
    {
        var enrichment = new EnrichmentOptions();
        enrichment.Indices.Preferences.IndoorTemp = 18.0;
        var njordOptions = new NjordOptions { Locations = [new() { Name = "lucerne" }] };
        var feature = new IndexEnrichment(
            Options.Create(enrichment), Options.Create(njordOptions), Parameters, new FakeTimeProvider(T0));

        var consensus = BuildTwoModelConsensus("lucerne", temp: 10.0);

        // null sensors => config value (18.0) is used, not the hardcoded default (22.0)
        var defaultFeature = CreateFeature(); // uses default config (IndoorTemp=null => 22.0)
        var eventsCustom = feature.Compute(consensus).ToList();
        var eventsDefault = defaultFeature.Compute(consensus).ToList();

        var resultCustom = Assert.IsType<IndexResult>(Assert.IsType<EgressEvent.EnrichmentUpdate>(Assert.Single(eventsCustom)).Result);
        var resultDefault = Assert.IsType<IndexResult>(Assert.IsType<EgressEvent.EnrichmentUpdate>(Assert.Single(eventsDefault)).Result);

        // Config IndoorTemp=18 vs default 22 should produce different ventilation scores
        Assert.NotEqual(resultCustom.Ventilation, resultDefault.Ventilation);
    }
}
