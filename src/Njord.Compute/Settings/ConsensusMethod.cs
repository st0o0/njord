using Njord.Compute.Consensus;
using Njord.Compute.Contracts;
namespace Njord.Compute.Settings;

public enum ConsensusMethod
{
    Mean,
    Median,
    TrimmedMean,
}
