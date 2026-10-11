using Microsoft.Extensions.Options;
using Njord.Compute.Analysis;
using Njord.Compute.Configuration;

namespace Njord.Alerts;

public sealed class AlertAnalyzer
{
    private readonly AlertOptions _options;
    private readonly TimeProvider _timeProvider;

    public AlertAnalyzer(IOptions<AlertOptions> options, TimeProvider timeProvider)
    {
        _options = options.Value;
        _timeProvider = timeProvider;
    }

    public bool Enabled => _options.Enabled;

    public AlertResult? Compute(ConsensusSnapshot consensus)
    {
        if (!Enabled)
            return null;

        return AlertEvaluator.EvaluateAll(consensus, _options, _timeProvider);
    }
}
