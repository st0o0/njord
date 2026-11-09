using System.Collections.Immutable;
using Njord.Core;
using Njord.Messages.Results;
using Njord.Messages.Stream;

namespace Njord.Grpc;

internal sealed record GrpcConsumerState(
    ImmutableDictionary<(Location Location, DomainKind Domain, string? ModelId), PipelineEvent> DomainData,
    ImmutableDictionary<(Location Location, Type Type), FeatureResult> Results,
    ImmutableDictionary<string, EntityState> EntityStates,
    ImmutableHashSet<string> StaleEntities)
{
    public static readonly GrpcConsumerState Empty = new(
        [],
        [],
        [],
        []);
}
