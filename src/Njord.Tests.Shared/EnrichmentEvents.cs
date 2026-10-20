using Njord.Forecast;
using Njord.Messages.Alerts;
using Njord.Messages.Pipeline;

namespace Njord.Tests.Shared;

public static class EnrichmentEvents
{
    public static PipelineEvent.AlertRaised Alerts(Location location, DateTimeOffset at) =>
        new(location, new Alert(AlertType.Heat, AlertSeverity.Yellow, 0.75, NoDetails.Instance), at);
}
