using Njord.Core.Domain;
using Njord.Messages.Alerts;
using Njord.Messages.Pipeline;
using Njord.Model;

namespace Njord.AirQuality;

public sealed class AirQualityAlertHandler(IReadOnlyList<IAirQualityAlertRule> rules, AlertSettings settings, bool enabled)
    : IDomainHandler
{
    public IReadOnlySet<DomainKind> Triggers { get; } = new HashSet<DomainKind> { DomainKind.AirQuality };

    public IReadOnlyList<PipelineEvent> Handle(LocationForecast current, LocationForecast? previous, TimeProvider time)
    {
        if (!enabled || current.AirQuality is not { } airQuality)
        {
            return [];
        }

        var now = time.GetUtcNow();
        return
        [
            .. rules
                .Select(rule => rule.Evaluate(airQuality, settings, now))
                .Where(alert => alert.Severity != AlertSeverity.None)
                .Select(alert => new PipelineEvent.AlertRaised(current.Location, alert, now)),
        ];
    }
}
