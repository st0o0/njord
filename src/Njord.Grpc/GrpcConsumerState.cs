using System.Collections.Immutable;
using Njord.Core;
using Njord.Messages.Results;
using Njord.Messages.Stream;

namespace Njord.Grpc;

internal sealed record GrpcConsumerState(
    ImmutableDictionary<(Location Location, DomainKind Domain, string? ModelId), PipelineEvent> DomainData,
    ImmutableDictionary<(Location Location, Type Type), FeatureResult> Results)
{
    public static readonly GrpcConsumerState Empty = new(
        ImmutableDictionary<(Location, DomainKind, string?), PipelineEvent>.Empty,
        ImmutableDictionary<(Location, Type), FeatureResult>.Empty);
}
