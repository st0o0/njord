using Microsoft.Extensions.Options;
using Njord.Configuration;
using Njord.Domain.Analysis;
using Njord.Domain.Weather;
using Njord.Egress;
using Njord.Enrichment.Features;

namespace Njord.Tests.Enrichment;

public sealed class ConsensusEnrichmentSpec
{
    private static readonly DateTimeOffset Now = new(2026, 7, 15, 12, 0, 0, TimeSpan.Zero);

    private static ConsensusEnrichment CreateEnrichment(bool enabled = true)
    {
        var njordOptions = new NjordOptions { ForecastDays = 4 };
        var enrichmentOptions = new EnrichmentOptions
        {
            Consensus = new ConsensusOptions { Enabled = enabled },
        };
        var parameters = ParameterRegistry.Resolve(["Weather"], [], []);
        var time = new FakeTimeProvider(Now);

        return new ConsensusEnrichment(
            Options.Create(njordOptions),
            Options.Create(enrichmentOptions),
            parameters,
            time);
    }

    private static ModelSnapshot CreateSnapshot(params (string model, int maxHours)[] models)
    {
        var temp = ParameterRegistry.GetByApiName("temperature_2m")!;
        var snapshot = ModelSnapshot.Empty;

        foreach (var (modelId, maxHours) in models)
        {
            var points = new List<ForecastPoint>();
            for (var h = 0; h <= maxHours; h++)
            {
                points.Add(new ForecastPoint(
                    Now.AddHours(h),
                    new Dictionary<ParameterDef, double?> { [temp] = 20.0 + h * 0.1 }));
            }

            var forecast = new ModelForecast(
                new WeatherModel(modelId), "lucerne", new CycleId(Now),
                new ForecastSeries(points), DailyForecastSeries.Empty);
            snapshot = snapshot.Update(forecast);
        }

        return snapshot;
    }

    [Fact(Timeout = 5000)]
    public void Disabled_when_configured()
    {
        var enrichment = CreateEnrichment(enabled: false);
        Assert.False(enrichment.Enabled);
    }

    [Fact(Timeout = 5000)]
    public void Enabled_by_default()
    {
        var enrichment = CreateEnrichment(enabled: true);
        Assert.True(enrichment.Enabled);
        Assert.Equal("consensus", enrichment.TypeName);
    }

    [Fact(Timeout = 5000)]
    public void Computes_consensus_up_to_second_shortest_model()
    {
        var enrichment = CreateEnrichment();
        var snapshot = CreateSnapshot(("icon_d2", 48), ("gfs", 120), ("ecmwf", 240));

        var events = enrichment.Compute(snapshot, ["lucerne"]).ToList();

        Assert.Single(events);
        var result = (ConsensusResult)
            ((EgressEvent.EnrichmentUpdate)events[0]).Result;

        var tempConsensus = result.Parameters
            .FirstOrDefault(p => p.Parameter.ApiName == "temperature_2m");
        Assert.NotNull(tempConsensus);

        Assert.True(tempConsensus.ByHorizon.ContainsKey("h0"));
        Assert.True(tempConsensus.ByHorizon.ContainsKey("h48"));
        Assert.True(tempConsensus.ByHorizon.ContainsKey("h120"));
        Assert.False(tempConsensus.ByHorizon.ContainsKey("h121"));
    }

    [Fact(Timeout = 5000)]
    public void Skips_location_with_fewer_than_two_models()
    {
        var enrichment = CreateEnrichment();
        var snapshot = CreateSnapshot(("icon_d2", 48));

        var events = enrichment.Compute(snapshot, ["lucerne"]).ToList();
        Assert.Empty(events);
    }

    [Fact(Timeout = 5000)]
    public void All_hours_have_at_least_two_models()
    {
        var enrichment = CreateEnrichment();
        var snapshot = CreateSnapshot(("icon_d2", 48), ("gfs", 120));

        var events = enrichment.Compute(snapshot, ["lucerne"]).ToList();
        var result = (ConsensusResult)
            ((EgressEvent.EnrichmentUpdate)events[0]).Result;

        foreach (var pc in result.Parameters)
        {
            foreach (var (_, hc) in pc.ByHorizon)
            {
                Assert.True(hc.AvailableModels.Count >= 2,
                    $"Hour should have ≥2 models, got {hc.AvailableModels.Count}");
            }
        }
    }

    private sealed class FakeTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }
}
