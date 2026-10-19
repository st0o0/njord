using Google.Protobuf.WellKnownTypes;
using Njord.Consensus;
using Njord.Grpc.V2;
using Njord.Messages.Alerts;
using Njord.Messages.Pipeline;
using Njord.Model;

namespace Njord.Grpc.Mapping;

internal static class EnrichmentEventMappings
{
    public static EnrichmentEvent? ToEnrichmentEvent(this PipelineEvent pipelineEvent)
    {
        return pipelineEvent switch
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
                Alerts = new AlertResult(new LocationId(r.Location.Name), [r.Alert]).ToProto(),
            },
            PipelineEvent.IndexComputed i => new EnrichmentEvent
            {
                Location = i.Result.Location.Name,
                TypeName = i.Kind.ToWireName(),
                UpdatedAt = i.Timestamp.ToTimestamp(),
                Indices = i.Result.ToProto(),
            },
            PipelineEvent.TrendComputed t => new EnrichmentEvent
            {
                Location = t.Result.Location.Name,
                TypeName = t.Kind.ToWireName(),
                UpdatedAt = t.Timestamp.ToTimestamp(),
                Trends = t.Result.ToProto(),
            },
            PipelineEvent.DerivedComputed d => new EnrichmentEvent
            {
                Location = d.Result.Location.Name,
                TypeName = d.Kind.ToWireName(),
                UpdatedAt = d.Timestamp.ToTimestamp(),
                Derived = d.Result.ToProto(),
            },
            PipelineEvent.ConsensusComputed c => c.ToEnrichmentEvent(),
            _ => null,
        };
    }

    public static GetEnrichmentsResponse ToProto(this LatestAnalysisResponse response)
    {
        switch (response)
        {
            case LatestAnalysisNotAvailable missing:
                return new GetEnrichmentsResponse { Location = missing.Location.Name };
            case LatestAnalysisResult found:
                var proto = new GetEnrichmentsResponse { Location = found.Location.Name };
                if (found.Consensus is { } consensus)
                {
                    proto.Consensus = consensus.ToConsensusResult().ToProto();
                    proto.ConsensusUpdatedAt = consensus.Timestamp.ToTimestamp();
                }

                if (found.Alerts is { } alerts)
                {
                    proto.Alerts = alerts.Result.ToProto();
                }

                if (found.Derived is { } derived)
                {
                    proto.Derived = derived.Result.ToProto();
                }

                if (found.Trends is { } trends)
                {
                    proto.Trends = trends.Result.ToProto();
                }

                if (found.Indices is { } indices)
                {
                    proto.Indices = indices.Result.ToProto();
                }

                return proto;
            default:
                throw new ArgumentOutOfRangeException(nameof(response), response.GetType().Name, "Unknown analysis response");
        }
    }

    private static EnrichmentEvent ToEnrichmentEvent(this PipelineEvent.ConsensusComputed c) =>
        new()
        {
            Location = c.Consensus.Location.Name,
            TypeName = c.Kind.ToWireName(),
            UpdatedAt = c.Timestamp.ToTimestamp(),
            Consensus = c.ToConsensusResult().ToProto(),
        };

    private static ConsensusResult ToConsensusResult(this PipelineEvent.ConsensusComputed c) =>
        new(c.Consensus.Hourly.Parameters, c.Consensus.Daily.Parameters, c.Consensus.ComputedAt);
}
