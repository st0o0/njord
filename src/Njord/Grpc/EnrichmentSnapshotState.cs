using System.Collections.Immutable;
using Njord.Persistence;

namespace Njord.Grpc;

public sealed record EnrichmentSnapshotState(
    ImmutableDictionary<string, object> Enrichments,
    int UpdatesSinceSnapshot)
{
    public static readonly EnrichmentSnapshotState Empty = new(
        ImmutableDictionary<string, object>.Empty, 0);
}

public static class EnrichmentSnapshotStateExtensions
{
    public static EnrichmentSnapshotState Apply(this EnrichmentSnapshotState state, string key, object result) =>
        state with
        {
            Enrichments = state.Enrichments.SetItem(key, result),
            UpdatesSinceSnapshot = state.UpdatesSinceSnapshot + 1,
        };

    public static EnrichmentSnapshotState ResetSnapshotCounter(this EnrichmentSnapshotState state) =>
        state with { UpdatesSinceSnapshot = 0 };

    public static EnrichmentQueryResponse GetEnrichment(this EnrichmentSnapshotState state, string key) =>
        state.Enrichments.TryGetValue(key, out var result)
            ? new EnrichmentFound(result)
            : new EnrichmentNotFound(key);

    public static AllEnrichmentsResult GetAllEnrichments(this EnrichmentSnapshotState state, string location)
    {
        var prefix = $"{location}|";
        var results = state.Enrichments
            .Where(kvp => kvp.Key.StartsWith(prefix))
            .Select(kvp => (TypeName: kvp.Key[prefix.Length..], kvp.Value))
            .ToList();
        return new AllEnrichmentsResult(results);
    }

    public static EnrichmentSnapshotDto GetPersistenceState(this EnrichmentSnapshotState state) =>
        EnrichmentSnapshotMapping.ToDto(new Dictionary<string, object>(state.Enrichments));

    public static EnrichmentSnapshotState FromPersistence(EnrichmentSnapshotDto dto) =>
        new(EnrichmentSnapshotMapping.ToDomain(dto).ToImmutableDictionary(), 0);

    public static string MakeKey(string location, string typeName) => $"{location}|{typeName}";
}
