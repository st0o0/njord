using Njord.Consensus.Settings;

namespace Njord.Core.Configuration.Consensus;

public static class ConsensusSettingsConversion
{
    public static ConsensusSettings ToSettings(this ConsensusOptions options) =>
        new(options.Method, options.TrimPercent, options.MinQuorum);
}
