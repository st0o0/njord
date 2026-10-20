using Google.Protobuf.WellKnownTypes;
using Njord.Grpc.V2;
using Njord.Messages.Alerts;
using Njord.Messages.Pipeline;
using Njord.Model;

namespace Njord.Grpc.Mapping;

internal static class EnrichmentEventMappings
{
    public static EnrichmentEvent? ToEnrichmentEvent(this PipelineEvent pipelineEvent, DateTimeOffset now) => pipelineEvent switch
    {
        PipelineEvent.AlertRaised r => new EnrichmentEvent
        {
            Location = r.Location.Name,
            TypeName = EnrichmentNames.Alerts,
            UpdatedAt = r.Timestamp.ToTimestamp(),
            Alerts = new AlertResult(r.Location, [r.Alert]).ToProto(),
        },
        _ when pipelineEvent.ToLifestyleUpdate() is { } lifestyle => new EnrichmentEvent
        {
            Location = lifestyle.Location.Name,
            TypeName = EnrichmentNames.Lifestyle,
            UpdatedAt = now.ToTimestamp(),
            Lifestyle = lifestyle.ToProto(),
        },
        _ => null,
    };

    public static Location? ToEnrichmentLocation(this PipelineEvent pipelineEvent) => pipelineEvent switch
    {
        PipelineEvent.AlertRaised r => r.Location,
        _ => pipelineEvent.ToLifestyleUpdate()?.Location,
    };

    public static V2.LifestyleUpdate ToProto(this Messages.Pipeline.LifestyleUpdate update)
    {
        var proto = new V2.LifestyleUpdate { Type = update.Kind.ToWireName() };
        foreach (var (key, value) in update.Data)
        {
            proto.Data[key] = value;
        }

        return proto;
    }

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

                proto.Lifestyle.AddRange((found.Lifestyle ?? []).Select(update => update.ToProto()));

                return proto;
            default:
                throw new ArgumentOutOfRangeException(nameof(response), response.GetType().Name, "Unknown analysis response");
        }
    }
}
