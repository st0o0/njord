using Njord.Consensus.Settings;

namespace Njord.Pipeline.Configuration;

public sealed class ConsensusOptions
{
    public const string SectionName = "Njord:Enrichment:Consensus";

    public bool Enabled { get; set; }
    public ConsensusMethod Method { get; set; }
    public double TrimPercent { get; set; }
    public int MinQuorum { get; set; }
}
