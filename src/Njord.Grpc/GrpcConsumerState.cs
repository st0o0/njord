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

internal static class GrpcConsumerStateExtensions
{
    public static GrpcConsumerState Apply(this GrpcConsumerState state, PipelineEvent evt)
    {
        if (evt.ToDomainKind() is { } domain && evt.ToDomainLocation() is { } location)
        {
            return state with { DomainData = state.DomainData.SetItem((location, domain), evt) };
        }

        return evt is FeatureResult result
            ? state with { Results = state.Results.SetItem((result.Location, result.GetType()), result) }
            : state;
    }

    public static PipelineEvent? GetDomainData(this GrpcConsumerState state, Location location, DomainKind domain) =>
        state.DomainData.GetValueOrDefault((location, domain));

    public static IReadOnlyList<FeatureResult> GetLatestResults(this GrpcConsumerState state, Location location) =>
        state.Results
            .Where(entry => entry.Key.Location == location)
            .OrderBy(entry => entry.Key.Type.Name, StringComparer.Ordinal)
            .Select(entry => entry.Value)
            .ToList();
}
