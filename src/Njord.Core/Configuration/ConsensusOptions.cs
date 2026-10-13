using Njord.Compute.Configuration;

namespace Njord.Core.Configuration;

public sealed class ConsensusOptions
{
    public bool Enabled { get; set; } = true;
    public ConsensusMethod Method { get; set; } = ConsensusMethod.Median;
    public double TrimPercent { get; set; } = 0.1;
}
