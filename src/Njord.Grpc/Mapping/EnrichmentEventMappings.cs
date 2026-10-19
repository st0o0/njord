using Google.Protobuf.WellKnownTypes;
using Njord.Grpc.V2;
using Njord.Messages.Alerts;
using Njord.Messages.Pipeline;
using Njord.Model;

namespace Njord.Grpc.Mapping;

internal static class EnrichmentEventMappings
{
    public static EnrichmentEvent? ToEnrichmentEvent(this PipelineEvent pipelineEvent) => pipelineEvent switch
    {
        PipelineEvent.AlertComputed a => new EnrichmentEvent
        {
            Location = a.Result.Location.Name,
            TypeName = a.Kind.ToWireName(),
            UpdatedAt = a.Timestamp.ToTimestamp(),
            Alerts = a.Result.ToProto(),
        },
        PipelineEvent.AlertRaised r => new EnrichmentEvent
        {
            Location = r.Location.Name,
            TypeName = r.Kind.ToWireName(),
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
                    proto.Alerts = alerts.Result.ToProto();
                }

                return proto;
            default:
                throw new ArgumentOutOfRangeException(nameof(response), response.GetType().Name, "Unknown analysis response");
        }
    }
}
