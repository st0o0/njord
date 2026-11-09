using System.Collections.Immutable;
using Njord.Core;
using Njord.Grpc.Mapping;
using Njord.Messages.Results;
using Njord.Messages.Stream;

namespace Njord.Grpc;

internal static class GrpcConsumerStateExtensions
{
    public static GrpcConsumerState Apply(this GrpcConsumerState state, PipelineEvent evt)
    {
        if (evt.ToDomainKind() is { } domain && evt.ToDomainLocation() is { } location)
        {
            return state with { DomainData = state.DomainData.SetItem((location, domain, evt.ToModelId()), evt) };
        }

        return evt is FeatureResult result
            ? state with { Results = state.Results.SetItem((result.Location, result.GetType()), result) }
            : state;
    }

    public static PipelineEvent? GetDomainData(this GrpcConsumerState state, Location location, DomainKind domain, string? modelId = null) =>
        state.DomainData.GetValueOrDefault((location, domain, modelId));

    public static IReadOnlyList<FeatureResult> GetLatestResults(this GrpcConsumerState state, Location location) =>
        state.Results
            .Where(entry => entry.Key.Location == location)
            .OrderBy(entry => entry.Key.Type.Name, StringComparer.Ordinal)
            .Select(entry => entry.Value)
            .ToList();

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
