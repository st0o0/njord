using Google.Protobuf.WellKnownTypes;
using Njord.Forecast;
using Njord.Grpc.V2;
using Njord.Messages.Pipeline;

namespace Njord.Grpc.Mapping;

internal static class EnrichmentEventMappings
{
    public static EnrichmentEvent? ToEnrichmentEvent(this PipelineEvent pipelineEvent, DateTimeOffset now) => pipelineEvent switch
    {
        AlertResult r => new EnrichmentEvent
        {
            Location = r.Location.Name,
            TypeName = EnrichmentNames.Alerts,
            UpdatedAt = r.Timestamp.ToTimestamp(),
            Alerts = new[] { r }.ToProto(),
        },
        FeatureResult r => new EnrichmentEvent
        {
            Location = r.Location.Name,
            TypeName = EnrichmentNames.Lifestyle,
            UpdatedAt = now.ToTimestamp(),
            Lifestyle = r.ToProto(),
        },
        _ => null,
    };

    public static Location? ToEnrichmentLocation(this PipelineEvent pipelineEvent) =>
        (pipelineEvent as FeatureResult)?.Location;

    public static GetEnrichmentsResponse ToProto(this LatestAnalysisResponse response)
    {
        switch (response)
        {
            case LatestAnalysisNotAvailable missing:
                return new GetEnrichmentsResponse { Location = missing.Location.Name };
            case LatestAnalysisResult found:
                var proto = new GetEnrichmentsResponse { Location = found.Location.Name };
                var alerts = found.Results.OfType<AlertResult>().ToList();
                if (alerts.Count > 0)
                {
                    proto.Alerts = alerts.ToProto();
                }

                proto.Lifestyle.AddRange(found.Results.Where(r => r is not AlertResult).Select(r => r.ToProto()));

                return proto;
            default:
                throw new ArgumentOutOfRangeException(nameof(response), response.GetType().Name, "Unknown analysis response");
        }
    }
}
