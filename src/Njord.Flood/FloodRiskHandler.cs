using Microsoft.Extensions.Options;
using Njord.Core.Configuration;
using Njord.Core.Domain;
using Njord.Messages.Alerts;
using Njord.Messages.Pipeline;
using Njord.Model;

namespace Njord.Flood;

public sealed class FloodRiskHandler(IAlertRule<DomainForecast<FloodPoint>> rule, IOptions<AlertOptions> options) : IDomainHandler
{
    public string ConfigKey => "FloodRisk";

    public IReadOnlySet<DomainKind> Triggers { get; } = new HashSet<DomainKind> { DomainKind.Flood };

    public IReadOnlyList<PipelineEvent> Handle(LocationForecast current, LocationForecast? previous, TimeProvider time)
    {
        if (!options.Value.Enabled || current.Flood is not { } flood)
        {
            return [];
        }

        var now = time.GetUtcNow();
        var alert = rule.Evaluate(flood, options.Value, now);
        return alert.Severity == AlertSeverity.None ? [] : [new PipelineEvent.AlertRaised(current.Location, alert, now)];
    }
}
