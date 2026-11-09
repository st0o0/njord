using System.Collections.Immutable;
using Njord.Messages.Results;

namespace Njord.Grpc;

internal static class GrpcConsumerStateExtensions
{
    public static GrpcConsumerState ApplyEntity(this GrpcConsumerState state, string entityId, FeatureResult result, DateTimeOffset now) =>
        state with { EntityStates = state.EntityStates.SetItem(entityId, new EntityState(entityId, result, now)) };

    public static (GrpcConsumerState State, IReadOnlyList<string> Unavailable, IReadOnlyList<string> Available) CheckAvailability(
        this GrpcConsumerState state, DateTimeOffset now, TimeSpan threshold)
    {
        var stale = state.EntityStates.Values
            .Where(e => e.LastUpdated + threshold < now)
            .Select(e => e.EntityId)
            .ToImmutableHashSet();
        var unavailable = stale.Except(state.StaleEntities).Order(StringComparer.Ordinal).ToList();
        var available = state.StaleEntities.Except(stale).Order(StringComparer.Ordinal).ToList();
        var next = state with
        {
            StaleEntities = stale,
            EntityStates = state.EntityStates.SetItems(state.EntityStates
                .Select(kv => KeyValuePair.Create(kv.Key, kv.Value with { IsAvailable = !stale.Contains(kv.Key) }))),
        };
        return (next, unavailable, available);
    }
}
