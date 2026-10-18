using Njord.Consensus.Settings;

namespace Njord.Pipeline.Configuration;

public static class ConsensusSettingsConversion
{
    public static ConsensusSettings ToSettings(this ConsensusOptions options) =>
        new(options.Method, options.TrimPercent, options.MinQuorum);
}
