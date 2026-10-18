using Njord.Consensus.Settings;
using Njord.Core.Configuration.Consensus;
using Njord.Tests.Shared;

namespace Njord.Core.Tests.Configuration.Consensus;

public sealed class ConsensusOptionsValidatorSpec
{
    private static ConsensusOptions Default() => AppSettingsDefaults.Options<ConsensusOptions>();

    [Theory]
    [InlineData(ConsensusMethod.Mean)]
    [InlineData(ConsensusMethod.Median)]
    [InlineData(ConsensusMethod.TrimmedMean)]
    public void consensus_valid_methods_accepted(ConsensusMethod method)
    {
        var opts = Default();
        opts.Method = method;
        var result = new ConsensusOptionsValidator().Validate(null, opts);
        Assert.True(result.Succeeded);
    }

    [Fact]
    public void consensus_invalid_method_rejected()
    {
        var opts = Default();
        opts.Method = (ConsensusMethod)999;
        var result = new ConsensusOptionsValidator().Validate(null, opts);
        Assert.True(result.Failed);
        Assert.Contains("999", result.FailureMessage);
    }

    [Fact]
    public void consensus_trimmed_mean_with_valid_trim_percent_accepted()
    {
        var opts = Default();
        opts.Method = ConsensusMethod.TrimmedMean;
        opts.TrimPercent = 0.1;
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
        opts.Method = ConsensusMethod.TrimmedMean;
        opts.TrimPercent = trimPercent;
        var result = new ConsensusOptionsValidator().Validate(null, opts);
        Assert.True(result.Failed);
        Assert.Contains("TrimPercent", result.FailureMessage);
    }

    [Fact]
    public void consensus_non_trimmed_mean_ignores_trim_percent()
    {
        var opts = Default();
        opts.Method = ConsensusMethod.Median;
        opts.TrimPercent = 0.9;
        var result = new ConsensusOptionsValidator().Validate(null, opts);
        Assert.True(result.Succeeded);
    }


    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void consensus_quorum_below_one_rejected(int quorum)
    {
        var opts = Default();
        opts.MinQuorum = quorum;
        var result = new ConsensusOptionsValidator().Validate(null, opts);
        Assert.True(result.Failed);
        Assert.Contains("MinQuorum", result.FailureMessage);
    }

    [Fact]
    public void consensus_quorum_defaults_to_two_models()
    {
        Assert.Equal(2, Default().MinQuorum);
    }
}
