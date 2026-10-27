using Njord.Core;
using Njord.Messages.Alerts;
using Njord.Messages.Pipeline;

namespace Njord.Tests.Shared;

public static class EnrichmentEvents
{
    public static HeatAlertResult Alerts(Location location, DateTimeOffset at) =>
        new(location, AlertSeverity.Yellow, 0.75, 32.0, 30.0, null, null, null, ConfidenceLevel.Medium, 33.0);
}
