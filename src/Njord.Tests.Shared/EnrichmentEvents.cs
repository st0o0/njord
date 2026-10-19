using Njord.Messages.Alerts;
using Njord.Messages.Pipeline;
using Njord.Model;

namespace Njord.Tests.Shared;

public static class EnrichmentEvents
{
    public static PipelineEvent.AlertComputed Alerts(LocationId location, DateTimeOffset at) =>
        new(new AlertResult(location, []), at);
}
