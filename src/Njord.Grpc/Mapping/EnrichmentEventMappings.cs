using Google.Protobuf.WellKnownTypes;
using Njord.Grpc.V2;
using Njord.Messages.Alerts;
using Njord.Messages.Pipeline;

namespace Njord.Grpc.Mapping;

internal static class EnrichmentEventMappings
{
    public static EnrichmentEvent? ToEnrichmentEvent(this PipelineEvent pipelineEvent) => pipelineEvent switch
    {
        PipelineEvent.AlertRaised r => new EnrichmentEvent
        {
            Location = r.Location.Name,
            TypeName = EnrichmentNames.Alerts,
            UpdatedAt = r.Timestamp.ToTimestamp(),
            Alerts = new AlertResult(r.Location, [r.Alert]).ToProto(),
        },
        _ => null,
    };

    public static GetEnrichmentsResponse ToProto(this LatestAnalysisResponse response)
    {
        switch (response)
        {
            case LatestAnalysisNotAvailable missing:
                return new GetEnrichmentsResponse { Location = missing.Location.Name };
            case LatestAnalysisResult found:
                var proto = new GetEnrichmentsResponse { Location = found.Location.Name };
                if (found.Alerts is { } alerts)
                {
                    proto.Alerts = alerts.ToProto();
                }

                return proto;
            default:
                throw new ArgumentOutOfRangeException(nameof(response), response.GetType().Name, "Unknown analysis response");
        }
    }
}
