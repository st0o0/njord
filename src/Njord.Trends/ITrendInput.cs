using Njord.Compute.Analysis;

namespace Njord.Trends;

public interface ITrendInput
{
    ConsensusSnapshot Consensus { get; }
    ConsensusSnapshot? Previous { get; }
}
