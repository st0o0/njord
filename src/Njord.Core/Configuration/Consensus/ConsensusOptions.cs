using Njord.Compute.Configuration;

namespace Njord.Core.Configuration.Consensus;

public sealed class ConsensusOptions
{
    public const string SectionName = "Njord:Enrichment:Consensus";

    public bool Enabled { get; set; } = true;
    public ConsensusMethod Method { get; set; } = ConsensusMethod.Median;
    public double TrimPercent { get; set; } = 0.1;
    public int MinQuorum { get; set; } = 2;
}
