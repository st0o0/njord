using Njord.Core.Domain;
using Njord.Messages.Alerts;
using Njord.Messages.Pipeline;
using Njord.Model;

namespace Njord.Flood;

public sealed class FloodRiskHandler(FloodRiskAlertRule rule, AlertSettings settings, bool enabled) : IDomainHandler
{
    public IReadOnlySet<DomainKind> Triggers { get; } = new HashSet<DomainKind> { DomainKind.Flood };

    public IReadOnlyList<PipelineEvent> Handle(LocationForecast current, LocationForecast? previous, TimeProvider time)
    {
        if (!enabled || current.Flood is not { } flood)
        {
            return [];
        }

        var now = time.GetUtcNow();
        var alert = rule.Evaluate(flood, settings, now);
        return alert.Severity == AlertSeverity.None ? [] : [new PipelineEvent.AlertRaised(current.Location, alert, now)];
    }
}
