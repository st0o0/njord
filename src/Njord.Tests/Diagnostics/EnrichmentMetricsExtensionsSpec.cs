using System.Diagnostics.Metrics;
using Njord.Diagnostics;

namespace Njord.Tests.Diagnostics;

public sealed class EnrichmentMetricsExtensionsSpec
{
    [Fact]
    public void AddEnrichmentDuration_creates_histogram()
    {
        var histogram = NjordMetrics.Instance.AddEnrichmentDuration();

        Assert.Equal("njord_enrichment_duration_seconds", histogram.Name);
        Assert.Equal("s", histogram.Unit);
    }

    [Fact]
    public void AddConsensusModels_creates_gauge()
    {
        var gauge = NjordMetrics.Instance.AddConsensusModels();

        Assert.Equal("njord_consensus_models", gauge.Name);
    }

    [Fact]
    public void AddConsensusSpread_creates_gauge_with_celsius_unit()
    {
        var gauge = NjordMetrics.Instance.AddConsensusSpread();

        Assert.Equal("njord_consensus_spread_celsius", gauge.Name);
        Assert.Equal("Cel", gauge.Unit);
    }

    [Fact]
    public void AddHistoryMae_creates_gauge_with_celsius_unit()
    {
        var gauge = NjordMetrics.Instance.AddHistoryMae();

        Assert.Equal("njord_history_mae_celsius", gauge.Name);
        Assert.Equal("Cel", gauge.Unit);
    }

    [Fact]
    public void AddHistoryModelWeight_creates_gauge()
    {
        var gauge = NjordMetrics.Instance.AddHistoryModelWeight();

        Assert.Equal("njord_history_model_weight", gauge.Name);
        Assert.Equal("1", gauge.Unit);
    }
}
