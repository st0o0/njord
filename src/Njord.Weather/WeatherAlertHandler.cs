using Njord.Core.Domain;
using Njord.Messages.Alerts;
using Njord.Messages.Pipeline;
using Njord.Model;

namespace Njord.Weather.Alerts;

public sealed class WeatherAlertHandler(AlertAnalyzer analyzer, AlertSettings settings) : IDomainHandler
{
    public IReadOnlySet<DomainKind> Triggers { get; } = new HashSet<DomainKind> { DomainKind.Weather };

    public IReadOnlyList<PipelineEvent> Handle(LocationForecast current, LocationForecast? previous, TimeProvider time)
    {
        if (current.Weather is not { } weather)
        {
            return [];
        }

        var now = time.GetUtcNow();
        return analyzer.Compute(weather, settings, now) is { } alerts
            ? [.. alerts
                .Where(alert => alert.Severity != AlertSeverity.None)
                .Select(alert => new PipelineEvent.AlertRaised(current.Location, alert, now))]
            : [];
    }
}
