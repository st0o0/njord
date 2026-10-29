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
}
