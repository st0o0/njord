using Microsoft.Extensions.Options;
using Njord.Configuration;
using Njord.Domain.Weather;
using Njord.Egress;
using Njord.Enrichment.Features;

namespace Njord.Tests.Enrichment.Features;

public sealed class DerivedEnrichmentSpec
{
    private static readonly DateTimeOffset T0 = new(2026, 7, 14, 12, 0, 0, TimeSpan.Zero);

    private sealed class FakeTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }

    private static DerivedEnrichment CreateFeature()
    {
        var options = new NjordOptions
        {
            Locations = [new LocationOptions { Name = "lucerne", Latitude = 47.05, Longitude = 8.31 }],
            Models = ["icon_d2"],
        };
        var enrichment = new EnrichmentOptions();
        var parameters = ParameterRegistry.Resolve(["Weather"], [], []);

        return new DerivedEnrichment(
            Options.Create(options), Options.Create(enrichment), parameters, new FakeTimeProvider(T0));
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
                    [temp] = 22.0 + h * 0.1,
                    [wind] = 5.0,
                    [humidity] = 65.0,
                }));

        return new ModelForecast(new WeatherModel("icon_d2"), location, new CycleId(T0),
            new ForecastSeries(points), DailyForecastSeries.Empty);
    }

    [Fact(Timeout = 5000)]
    public void Snapshot_with_temperature_data_produces_derived_values()
    {
        var feature = CreateFeature();
        var snapshot = ModelSnapshot.Empty.Update(BuildForecast("lucerne"));

        var events = feature.Compute(snapshot, ["lucerne"]).ToList();

        var update = Assert.Single(events);
        var enrichment = Assert.IsType<EgressEvent.EnrichmentUpdate>(update);
        Assert.Equal("lucerne", enrichment.Location);
        Assert.Equal("derived", enrichment.TypeName);
        Assert.NotNull(enrichment.Result);
    }

    [Fact(Timeout = 5000)]
    public void Empty_snapshot_produces_event_with_empty_result()
    {
        var feature = CreateFeature();

        var events = feature.Compute(ModelSnapshot.Empty, ["lucerne"]).ToList();

        var update = Assert.Single(events);
        var enrichment = Assert.IsType<EgressEvent.EnrichmentUpdate>(update);
        Assert.Equal("derived", enrichment.TypeName);
    }
}
