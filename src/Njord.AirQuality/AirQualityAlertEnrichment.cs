using Njord.Alerts;
using Njord.Core.Domain;
using Njord.Messages.Alerts;
using Njord.Model;

namespace Njord.AirQuality;

public sealed class AirQualityAlertEnrichment(IReadOnlyList<IAlertRule<DomainForecast<AirQualityPoint>>> rules, AlertSettings settings, bool enabled)
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
                .Select(rule => rule.Evaluate(input.AirQuality, settings, now))
                .Where(alert => alert.Severity != AlertSeverity.None)
                .Select(alert => new AlertRaisedEvent(input.Location, alert, now)),
        ];
    }
}
