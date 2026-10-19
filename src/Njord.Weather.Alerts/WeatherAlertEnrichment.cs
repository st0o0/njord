using Njord.Alerts;
using Njord.Core.Domain;
using Njord.Messages.Alerts;

namespace Njord.Weather.Alerts;

public sealed class WeatherAlertEnrichment(AlertAnalyzer analyzer, AlertSettings settings) : IEnrichment<WeatherAlertInput>
{
    public IReadOnlyList<EnrichmentEvent> Evaluate(WeatherAlertInput input, TimeProvider time)
    {
        var now = time.GetUtcNow();
        var alerts = analyzer.Compute(input.Weather, settings, now);
        if (alerts is null)
        {
            return [];
        }

        return
        [
            .. alerts
                .Where(alert => alert.Severity != AlertSeverity.None)
                .Select(alert => new AlertRaisedEvent(input.Location, alert, now)),
        ];
    }
}
