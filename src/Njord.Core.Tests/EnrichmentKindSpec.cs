using Njord.Compute.Analysis;
using Njord.Core.Egress;
using Njord.Weather;

namespace Njord.Core.Tests;

public sealed class EnrichmentKindSpec
{
    [Fact]
    public void ConsensusComputed_implements_IEnrichmentEvent_with_correct_kind()
    {
        var evt = new PipelineEvent.ConsensusComputed(
            CreateConsensusSnapshot(), DateTimeOffset.UtcNow);

        var enrichment = Assert.IsType<IEnrichmentEvent>(evt, exactMatch: false);
        Assert.Equal(EnrichmentKind.Consensus, enrichment.Kind);
    }

    [Fact]
    public void AlertComputed_implements_IEnrichmentEvent_with_correct_kind()
    {
        var evt = new PipelineEvent.AlertComputed(
            new AlertResult(new LocationId("loc"), []), DateTimeOffset.UtcNow);

        var enrichment = Assert.IsType<IEnrichmentEvent>(evt, exactMatch: false);
        Assert.Equal(EnrichmentKind.Alerts, enrichment.Kind);
    }

    [Fact]
    public void DerivedComputed_implements_IEnrichmentEvent_with_correct_kind()
    {
        var evt = new PipelineEvent.DerivedComputed(
            new DerivedResult(new LocationId("loc"), new Dictionary<Horizon, HorizonDerived>(), new ScalarDerived(null, null, null)),
            DateTimeOffset.UtcNow);

        var enrichment = Assert.IsType<IEnrichmentEvent>(evt, exactMatch: false);
        Assert.Equal(EnrichmentKind.Derived, enrichment.Kind);
    }

    [Fact]
    public void TrendComputed_implements_IEnrichmentEvent_with_correct_kind()
    {
        var evt = new PipelineEvent.TrendComputed(
            new TrendResult(new LocationId("loc"), new Dictionary<string, ParameterTrend?>(), null,
                new PrecipTimingInfo(null, null),
                new ExtremaTimingInfo(null, null),
                null, null),
            DateTimeOffset.UtcNow);

        var enrichment = Assert.IsType<IEnrichmentEvent>(evt, exactMatch: false);
        Assert.Equal(EnrichmentKind.Trends, enrichment.Kind);
    }

    [Fact]
    public void IndexComputed_implements_IEnrichmentEvent_with_correct_kind()
    {
        var evt = new PipelineEvent.IndexComputed(
            new IndexResult(new LocationId("loc"), [], null, null), DateTimeOffset.UtcNow);

        var enrichment = Assert.IsType<IEnrichmentEvent>(evt, exactMatch: false);
        Assert.Equal(EnrichmentKind.Indices, enrichment.Kind);
    }

    [Fact]
    public void ToWireName_returns_lowercase()
    {
        Assert.Equal("consensus", EnrichmentKind.Consensus.ToWireName());
        Assert.Equal("alerts", EnrichmentKind.Alerts.ToWireName());
        Assert.Equal("derived", EnrichmentKind.Derived.ToWireName());
        Assert.Equal("trends", EnrichmentKind.Trends.ToWireName());
        Assert.Equal("indices", EnrichmentKind.Indices.ToWireName());
    }

    private static ConsensusSnapshot CreateConsensusSnapshot() =>
        new(new LocationId("loc"), new HourlyConsensus([], 0), new DailyConsensus([], 0), DateTimeOffset.UtcNow);
}
