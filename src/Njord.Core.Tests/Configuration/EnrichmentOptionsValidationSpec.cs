using Microsoft.Extensions.Options;
using Njord.Configuration;
using Njord.Enrichment;

namespace Njord.Core.Tests.Configuration;

public sealed class EnrichmentOptionsValidationSpec
{
    private static EnrichmentOptions Default() => new();

    // --- ConsensusOptionsValidator ---

    [Theory]
    [InlineData("Mean")]
    [InlineData("Median")]
    [InlineData("TrimmedMean")]
    public void consensus_valid_methods_accepted(string method)
    {
        var opts = Default();
        opts.Consensus.Method = method;
        var result = new ConsensusOptionsValidator().Validate(null, opts);
        Assert.True(result.Succeeded);
    }

    [Fact]
    public void consensus_invalid_method_rejected()
    {
        var opts = Default();
        opts.Consensus.Method = "InvalidMethod";
        var result = new ConsensusOptionsValidator().Validate(null, opts);
        Assert.True(result.Failed);
        Assert.Contains("InvalidMethod", result.FailureMessage);
    }

    [Fact]
    public void consensus_trimmed_mean_with_valid_trim_percent_accepted()
    {
        var opts = Default();
        opts.Consensus.Method = "TrimmedMean";
        opts.Consensus.TrimPercent = 0.1;
        var result = new ConsensusOptionsValidator().Validate(null, opts);
        Assert.True(result.Succeeded);
    }

    [Theory]
    [InlineData(0.0)]
    [InlineData(0.5)]
    [InlineData(0.6)]
    [InlineData(-0.1)]
    public void consensus_trimmed_mean_with_invalid_trim_percent_rejected(double trimPercent)
    {
        var opts = Default();
        opts.Consensus.Method = "TrimmedMean";
        opts.Consensus.TrimPercent = trimPercent;
        var result = new ConsensusOptionsValidator().Validate(null, opts);
        Assert.True(result.Failed);
        Assert.Contains("TrimPercent", result.FailureMessage);
    }

    [Fact]
    public void consensus_non_trimmed_mean_ignores_trim_percent()
    {
        var opts = Default();
        opts.Consensus.Method = "Median";
        opts.Consensus.TrimPercent = 0.9;
        var result = new ConsensusOptionsValidator().Validate(null, opts);
        Assert.True(result.Succeeded);
    }

    // --- HistoryOptionsValidator ---

    [Fact]
    public void history_valid_options_accepted()
    {
        var result = new HistoryOptionsValidator().Validate(null, Default());
        Assert.True(result.Succeeded);
    }

    [Fact]
    public void history_zero_snapshot_interval_rejected()
    {
        var opts = Default();
        opts.History.SnapshotInterval = 0;
        var result = new HistoryOptionsValidator().Validate(null, opts);
        Assert.True(result.Failed);
        Assert.Contains("SnapshotInterval", result.FailureMessage);
    }

    [Fact]
    public void history_negative_retention_days_rejected()
    {
        var opts = Default();
        opts.History.RetentionDays = -1;
        var result = new HistoryOptionsValidator().Validate(null, opts);
        Assert.True(result.Failed);
        Assert.Contains("RetentionDays", result.FailureMessage);
    }

    [Fact]
    public void history_zero_min_sample_size_rejected()
    {
        var opts = Default();
        opts.History.MinSampleSize = 0;
        var result = new HistoryOptionsValidator().Validate(null, opts);
        Assert.True(result.Failed);
        Assert.Contains("MinSampleSize", result.FailureMessage);
    }

    // --- EnrichmentTypeNames / IsEnabled ---

    [Fact]
    public void type_names_have_the_wire_values()
    {
        Assert.Equal("alerts", EnrichmentTypeNames.Alerts);
        Assert.Equal("derived", EnrichmentTypeNames.Derived);
        Assert.Equal("trends", EnrichmentTypeNames.Trends);
        Assert.Equal("indices", EnrichmentTypeNames.Indices);
        Assert.Equal("history", EnrichmentTypeNames.History);
        Assert.Equal("consensus", EnrichmentTypeNames.Consensus);
    }

    [Theory]
    [InlineData("alerts")]
    [InlineData("derived")]
    [InlineData("trends")]
    [InlineData("indices")]
    [InlineData("history")]
    [InlineData("consensus")]
    public void is_enabled_maps_each_toggle_to_its_type_name(string typeName)
    {
        var options = new EnrichmentOptions
        {
            Consensus = new ConsensusOptions { Enabled = typeName == "consensus" },
            Alerts = new() { Enabled = typeName == "alerts" },
            Derived = new() { Enabled = typeName == "derived" },
            Trends = new() { Enabled = typeName == "trends" },
            Indices = new() { Enabled = typeName == "indices" },
            History = new() { Enabled = typeName == "history" },
        };

        foreach (var name in new[] { "alerts", "derived", "trends", "indices", "history", "consensus" })
        {
            Assert.Equal(name == typeName, options.IsEnabled(name));
        }
    }

    [Fact]
    public void is_enabled_reflects_defaults()
    {
        var options = new EnrichmentOptions();

        Assert.True(options.IsEnabled(EnrichmentTypeNames.Consensus));
        Assert.True(options.IsEnabled(EnrichmentTypeNames.Alerts));
        Assert.True(options.IsEnabled(EnrichmentTypeNames.Derived));
        Assert.False(options.IsEnabled(EnrichmentTypeNames.Trends));
        Assert.False(options.IsEnabled(EnrichmentTypeNames.Indices));
        Assert.False(options.IsEnabled(EnrichmentTypeNames.History));
    }

    [Fact]
    public void is_enabled_throws_for_unknown_type_name()
    {
        var ex = Assert.Throws<ArgumentException>(() => new EnrichmentOptions().IsEnabled("unknown"));
        Assert.Contains("unknown", ex.Message);
    }
}
