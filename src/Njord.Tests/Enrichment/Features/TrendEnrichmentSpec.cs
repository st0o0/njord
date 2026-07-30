using Microsoft.Extensions.Options;
using Njord.Configuration;
using Njord.Domain.Analysis;
using Njord.Domain.Weather;
using Njord.Egress;
using Njord.Enrichment.Features;

namespace Njord.Tests.Enrichment.Features;

public sealed class TrendEnrichmentSpec
{
    private static readonly DateTimeOffset T0 = new(2026, 7, 14, 12, 0, 0, TimeSpan.Zero);
    private static readonly ParameterDef Temperature = ParameterRegistry.GetByApiName("temperature_2m")!;

    private static readonly ResolvedParameterSet Parameters = ParameterRegistry.Resolve(["Weather"], [], []);

    private static TrendEnrichment CreateFeature(bool enabled = true)
    {
        var enrichment = new EnrichmentOptions
        {
            Trends = new TrendOptions { Enabled = enabled },
        };
        return new TrendEnrichment(Options.Create(enrichment));
    }

    private static ModelSnapshot MakeSnapshot(double baseTemp = 20.0)
    {
        var points = Enumerable.Range(0, 48).Select(h =>
            new ForecastPoint(T0.AddHours(h), new Dictionary<ParameterDef, double?> { [Temperature] = baseTemp + h * 0.1 }))
            .ToList();
        var forecast = new ModelForecast(new("icon_d2"), "lucerne", new CycleId(T0),
            new ForecastSeries(points), DailyForecastSeries.Empty);
        return ModelSnapshot.Empty.Update(forecast);
    }

    [Fact(Timeout = 5000)]
    public void Compute_returns_empty_when_previous_is_null()
    {
        var feature = CreateFeature();
        var snapshot = MakeSnapshot();

        var consensus = ConsensusSnapshot.Compute(snapshot, Parameters, "lucerne", TimeProvider.System);
        var events = feature.Compute(consensus, null).ToList();

        Assert.Empty(events);
    }

    [Fact(Timeout = 5000)]
    public void Compute_produces_events_when_previous_exists()
    {
        var feature = CreateFeature();
        var prev = MakeSnapshot(18.0);
        var current = MakeSnapshot(22.0);

        var prevConsensus = ConsensusSnapshot.Compute(prev, Parameters, "lucerne", TimeProvider.System);
        var currentConsensus = ConsensusSnapshot.Compute(current, Parameters, "lucerne", TimeProvider.System);
        var events = feature.Compute(currentConsensus, prevConsensus).ToList();

        Assert.Single(events);
        var update = Assert.IsType<EgressEvent.EnrichmentUpdate>(events[0]);
        Assert.Equal("trends", update.TypeName);
        Assert.IsType<TrendResult>(update.Result);
    }

    [Fact(Timeout = 5000)]
    public void Enabled_reflects_options()
    {
        Assert.True(CreateFeature(enabled: true).Enabled);
        Assert.False(CreateFeature(enabled: false).Enabled);
    }
}
