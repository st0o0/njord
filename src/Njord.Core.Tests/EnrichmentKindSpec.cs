using Njord.Consensus;
using Njord.Messages.Alerts;
using Njord.Messages.Derived;
using Njord.Messages.Indices;
using Njord.Messages.Pipeline;
using Njord.Messages.Trends;
using Njord.Weather;

namespace Njord.Core.Tests;

public sealed class EnrichmentKindSpec
{
    private static readonly DateTimeOffset At = new(2026, 7, 14, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void ConsensusComputed_implements_IEnrichmentEvent_with_correct_kind()
    {
        var evt = new PipelineEvent.ConsensusComputed(
            CreateLocationConsensus(), At);

        var enrichment = Assert.IsType<IEnrichmentEvent>(evt, exactMatch: false);
        Assert.Equal(EnrichmentKind.Consensus, enrichment.Kind);
    }

    [Fact]
    public void AlertComputed_implements_IEnrichmentEvent_with_correct_kind()
    {
        var evt = new PipelineEvent.AlertComputed(
            new AlertResult(new LocationId("loc"), []), At);

        var enrichment = Assert.IsType<IEnrichmentEvent>(evt, exactMatch: false);
        Assert.Equal(EnrichmentKind.Alerts, enrichment.Kind);
    }

    [Fact]
    public void DerivedComputed_implements_IEnrichmentEvent_with_correct_kind()
    {
        var evt = new PipelineEvent.DerivedComputed(
            new DerivedResult(new LocationId("loc"), new Dictionary<Horizon, HorizonDerived>(), new ScalarDerived(null, null, null)),
            At);

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
            At);

        var enrichment = Assert.IsType<IEnrichmentEvent>(evt, exactMatch: false);
        Assert.Equal(EnrichmentKind.Trends, enrichment.Kind);
    }

    [Fact]
    public void IndexComputed_implements_IEnrichmentEvent_with_correct_kind()
    {
        var evt = new PipelineEvent.IndexComputed(
            new IndexResult(new LocationId("loc"), [], null, null), At);

        var enrichment = Assert.IsType<IEnrichmentEvent>(evt, exactMatch: false);
        Assert.Equal(EnrichmentKind.Indices, enrichment.Kind);
    }

    [Theory]
    [InlineData(EnrichmentKind.Consensus, "consensus")]
    [InlineData(EnrichmentKind.Alerts, "alerts")]
    [InlineData(EnrichmentKind.Derived, "derived")]
    [InlineData(EnrichmentKind.Trends, "trends")]
    [InlineData(EnrichmentKind.Indices, "indices")]
    public void ToWireName_returns_the_documented_name(EnrichmentKind kind, string expected)
    {
        Assert.Equal(expected, kind.ToWireName());
    }

    [Fact]
    public void ToWireName_is_pinned_for_every_EnrichmentKind_member()
    {
        var names = Enum.GetValues<EnrichmentKind>().Select(k => k.ToWireName()).ToList();

        Assert.Equal(["consensus", "alerts", "derived", "trends", "indices"], names);
    }

    [Fact]
    public void ToWireName_throws_for_an_undefined_value()
    {
        var ex = Assert.Throws<ArgumentOutOfRangeException>(() => ((EnrichmentKind)999).ToWireName());

        Assert.Equal("kind", ex.ParamName);
        Assert.Equal((EnrichmentKind)999, ex.ActualValue);
    }

    private static LocationConsensus CreateLocationConsensus() =>
        new(new LocationId("loc"), new HourlyConsensus([], 0), new DailyConsensus([], 0), At);
}
