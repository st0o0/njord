using Njord.Core.Domain;
using Njord.Messages.Alerts;
using Njord.Messages.Pipeline;
using Njord.Model;

namespace Njord.Flood;

public sealed class FloodRainHandler(FloodRainAlertRule rule, AlertSettings settings, bool enabled) : IDomainHandler
{
    public IReadOnlySet<DomainKind> Triggers { get; } = new HashSet<DomainKind> { DomainKind.Flood, DomainKind.Weather };

    public IReadOnlyList<PipelineEvent> Handle(LocationForecast current, LocationForecast? previous, TimeProvider time)
    {
        if (!enabled || current is not { Flood: { } flood, Weather: { } weather })
        {
            return [];
        }

        var now = time.GetUtcNow();
        var alert = rule.Evaluate(flood, weather, settings, now);
        return alert.Severity == AlertSeverity.None ? [] : [new PipelineEvent.AlertRaised(current.Location, alert, now)];
    }
}
