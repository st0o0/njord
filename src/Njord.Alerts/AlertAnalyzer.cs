using Microsoft.Extensions.Options;
using Njord.Compute.Consensus;
using Njord.Compute.Contracts;
using Njord.Compute.Settings;
using Njord.Core.Configuration.Alerts;

namespace Njord.Alerts;

public sealed class AlertAnalyzer
{
    private readonly TimeProvider _timeProvider;

    public AlertAnalyzer(IOptions<AlertOptions> options, TimeProvider timeProvider)
    {
        Enabled = options.Value.Enabled;
        _timeProvider = timeProvider;
    }

    public bool Enabled { get; }

    public AlertResult? Compute(ConsensusSnapshot consensus, AlertSettings settings)
    {
        if (!Enabled)
            return null;

        return AlertEvaluator.EvaluateAll(consensus, settings, _timeProvider);
    }
}
