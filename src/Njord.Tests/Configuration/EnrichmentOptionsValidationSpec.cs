using Microsoft.Extensions.Options;
using Njord.Configuration;

namespace Njord.Tests.Configuration;

public sealed class EnrichmentOptionsValidationSpec
{
    private static NjordOptions Default() => new();

    // --- ConsensusOptionsValidator ---

    [Theory(Timeout = 5000)]
    [InlineData("Mean")]
    [InlineData("Median")]
    [InlineData("TrimmedMean")]
    public void consensus_valid_methods_accepted(string method)
    {
        var opts = Default();
        opts.Enrichment.Consensus.Method = method;
        var result = new ConsensusOptionsValidator().Validate(null, opts);
        Assert.True(result.Succeeded);
    }

    [Fact(Timeout = 5000)]
    public void consensus_invalid_method_rejected()
    {
        var opts = Default();
        opts.Enrichment.Consensus.Method = "InvalidMethod";
        var result = new ConsensusOptionsValidator().Validate(null, opts);
        Assert.True(result.Failed);
        Assert.Contains("InvalidMethod", result.FailureMessage);
    }

    [Fact(Timeout = 5000)]
    public void consensus_trimmed_mean_with_valid_trim_percent_accepted()
    {
        var opts = Default();
        opts.Enrichment.Consensus.Method = "TrimmedMean";
        opts.Enrichment.Consensus.TrimPercent = 0.1;
        var result = new ConsensusOptionsValidator().Validate(null, opts);
        Assert.True(result.Succeeded);
    }

    [Theory(Timeout = 5000)]
    [InlineData(0.0)]
    [InlineData(0.5)]
    [InlineData(0.6)]
    [InlineData(-0.1)]
    public void consensus_trimmed_mean_with_invalid_trim_percent_rejected(double trimPercent)
    {
        var opts = Default();
        opts.Enrichment.Consensus.Method = "TrimmedMean";
        opts.Enrichment.Consensus.TrimPercent = trimPercent;
        var result = new ConsensusOptionsValidator().Validate(null, opts);
        Assert.True(result.Failed);
        Assert.Contains("TrimPercent", result.FailureMessage);
    }

    [Fact(Timeout = 5000)]
    public void consensus_non_trimmed_mean_ignores_trim_percent()
    {
        var opts = Default();
        opts.Enrichment.Consensus.Method = "Median";
        opts.Enrichment.Consensus.TrimPercent = 0.9;
        var result = new ConsensusOptionsValidator().Validate(null, opts);
        Assert.True(result.Succeeded);
    }

    // --- HistoryOptionsValidator ---

    [Fact(Timeout = 5000)]
    public void history_valid_options_accepted()
    {
        var result = new HistoryOptionsValidator().Validate(null, Default());
        Assert.True(result.Succeeded);
    }

    [Fact(Timeout = 5000)]
    public void history_zero_snapshot_interval_rejected()
    {
        var opts = Default();
        opts.Enrichment.History.SnapshotInterval = 0;
        var result = new HistoryOptionsValidator().Validate(null, opts);
        Assert.True(result.Failed);
        Assert.Contains("SnapshotInterval", result.FailureMessage);
    }

    [Fact(Timeout = 5000)]
    public void history_negative_retention_days_rejected()
    {
        var opts = Default();
        opts.Enrichment.History.RetentionDays = -1;
        var result = new HistoryOptionsValidator().Validate(null, opts);
        Assert.True(result.Failed);
        Assert.Contains("RetentionDays", result.FailureMessage);
    }

    [Fact(Timeout = 5000)]
    public void history_zero_min_sample_size_rejected()
    {
        var opts = Default();
        opts.Enrichment.History.MinSampleSize = 0;
        var result = new HistoryOptionsValidator().Validate(null, opts);
        Assert.True(result.Failed);
        Assert.Contains("MinSampleSize", result.FailureMessage);
    }
}
