using Njord.Core.Configuration;

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

}
