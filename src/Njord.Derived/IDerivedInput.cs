using Njord.Compute.Analysis;

namespace Njord.Derived;

public interface IDerivedInput
{
    ConsensusSnapshot Consensus { get; }
    IReadOnlyList<int> Horizons { get; }
}
