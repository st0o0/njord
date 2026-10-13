using Njord.Compute.Settings;
using Njord.Core.Configuration.Consensus;

namespace Njord.Core.Tests.Configuration.Consensus;

public sealed class ConsensusSettingsConversionSpec
{
    [Fact]
    public void Defaults_match_the_compute_defaults()
    {
        Assert.Equal(ConsensusSettings.Default, new ConsensusOptions().ToSettings());
    }

    [Fact]
    public void Values_are_carried_over()
    {
        var options = new ConsensusOptions { Method = ConsensusMethod.TrimmedMean, TrimPercent = 0.2, MinQuorum = 3 };

        Assert.Equal(new ConsensusSettings(ConsensusMethod.TrimmedMean, 0.2, 3), options.ToSettings());
    }
}
