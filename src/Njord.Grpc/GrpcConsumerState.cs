using System.Collections.Immutable;

namespace Njord.Grpc;

internal sealed record GrpcConsumerState(
    ImmutableDictionary<string, EntityState> EntityStates,
    ImmutableHashSet<string> StaleEntities)
{
    public static readonly GrpcConsumerState Empty = new(
        [],
        []);
}
