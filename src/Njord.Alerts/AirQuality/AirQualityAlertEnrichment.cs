using Njord.Core.Domain;
using Njord.Messages.Alerts;

namespace Njord.Alerts.AirQuality;

public sealed class AirQualityAlertEnrichment(IReadOnlyList<IAirQualityAlertRule> rules, bool enabled)
    : IEnrichment<AirQualityAlertInput>
{
    public IReadOnlyList<EnrichmentEvent> Evaluate(AirQualityAlertInput input, TimeProvider time)
    {
        if (!enabled)
        {
            return [];
        }

        var now = time.GetUtcNow();
        return
        [
            .. rules
                .Select(rule => rule.Evaluate(input.AirQuality, now))
                .Where(alert => alert.Severity != AlertSeverity.None)
                .Select(alert => new AirQualityAlertRaisedEvent(input.Location, alert, now)),
        ];
    }
}
