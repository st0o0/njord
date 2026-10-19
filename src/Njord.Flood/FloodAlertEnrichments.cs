using Njord.Alerts;
using Njord.Core.Domain;
using Njord.Messages.Alerts;

namespace Njord.Flood;

public sealed class FloodRiskAlertEnrichment(FloodRiskAlertRule rule, AlertSettings settings, bool enabled) : IEnrichment<FloodRiskInput>
{
    public IReadOnlyList<EnrichmentEvent> Evaluate(FloodRiskInput input, TimeProvider time)
    {
        if (!enabled)
        {
            return [];
        }

        var now = time.GetUtcNow();
        var alert = rule.Evaluate(input.Flood, settings, now);
        return alert.Severity == AlertSeverity.None ? [] : [new AlertRaisedEvent(input.Location, alert, now)];
    }
}

public sealed class FloodRainAlertEnrichment(FloodRainAlertRule rule, AlertSettings settings, bool enabled) : IEnrichment<FloodRainInput>
{
    public IReadOnlyList<EnrichmentEvent> Evaluate(FloodRainInput input, TimeProvider time)
    {
        if (!enabled)
        {
            return [];
        }

        var now = time.GetUtcNow();
        var alert = rule.Evaluate(input, settings, now);
        return alert.Severity == AlertSeverity.None ? [] : [new AlertRaisedEvent(input.Location, alert, now)];
    }
}
