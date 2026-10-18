using Njord.Compute.Consensus;
using Njord.Compute.Contracts;
using Njord.Compute.Settings;

namespace Njord.Alerts;

public interface IAlertRule
{
    AlertType Type { get; }

    Alert Evaluate(LocationConsensus consensus, AlertSettings settings);
}
