using Njord.Compute.Analysis;

namespace Njord.Alerts;

public interface IAlertInput
{
    ConsensusSnapshot Consensus { get; }
}
