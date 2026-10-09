using Njord.Compute.Analysis;
using Njord.Sensors;

namespace Njord.Indices;

public interface IIndexInput
{
    ConsensusSnapshot Consensus { get; }
    SensorSnapshot? Sensors { get; }
}
