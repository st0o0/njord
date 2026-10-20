using Njord.Forecast;
using Njord.Messages.Alerts;
using Njord.Messages.Pipeline;

namespace Njord.Tests.Shared;

public static class EnrichmentEvents
{
    public static AlertResult Alerts(Location location, DateTimeOffset at) =>
        new(location, "heat", new Alert(AlertSeverity.Yellow, 0.75, NoDetails.Instance), at);
}
