using System.Collections.Immutable;
using Njord.Forecast;
using Njord.Grpc.Mapping;
using Njord.Messages.Pipeline;

namespace Njord.Grpc;

internal sealed record GrpcConsumerState(
    ImmutableDictionary<(Location Location, DomainKind Domain), PipelineEvent> DomainData,
    ImmutableDictionary<(Location Location, Type Type), FeatureResult> Results)
{
    public static readonly GrpcConsumerState Empty = new(
        ImmutableDictionary<(Location, DomainKind), PipelineEvent>.Empty,
        ImmutableDictionary<(Location, Type), FeatureResult>.Empty);
}
