using Njord.Consensus;
using Njord.Messages.Alerts;

namespace Njord.Alerts;

public interface IAlertRule
{
    AlertType Type { get; }

    Alert Evaluate(LocationConsensus consensus, AlertSettings settings);
}
