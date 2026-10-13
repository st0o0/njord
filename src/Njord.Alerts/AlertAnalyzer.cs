using Microsoft.Extensions.Options;
using Njord.Compute.Analysis;
using Njord.Compute.Settings;
using Njord.Core.Configuration.Alerts;

namespace Njord.Alerts;

public sealed class AlertAnalyzer
{
    private readonly AlertSettings _settings;
    private readonly TimeProvider _timeProvider;

    public AlertAnalyzer(IOptions<AlertOptions> options, TimeProvider timeProvider)
    {
        Enabled = options.Value.Enabled;
        _settings = options.Value.ToSettings();
        _timeProvider = timeProvider;
    }

    public bool Enabled { get; }

    public AlertResult? Compute(ConsensusSnapshot consensus)
    {
        if (!Enabled)
            return null;

        return AlertEvaluator.EvaluateAll(consensus, _settings, _timeProvider);
    }
}
