using System.Collections.Immutable;
using Njord.Grpc.Mapping;
using Njord.Messages.Alerts;
using Njord.Messages.Pipeline;
using Njord.Model;

namespace Njord.Grpc;

internal sealed record GrpcConsumerState(
    ImmutableDictionary<(Location Location, DomainKind Domain), PipelineEvent> DomainData,
    ImmutableDictionary<Location, AlertResult> Alerts)
{
    public static readonly GrpcConsumerState Empty = new(
        ImmutableDictionary<(Location, DomainKind), PipelineEvent>.Empty,
        ImmutableDictionary<Location, AlertResult>.Empty);
}

internal static class GrpcConsumerStateExtensions
{
    public static GrpcConsumerState Apply(this GrpcConsumerState state, PipelineEvent evt)
    {
        if (evt.ToDomainKind() is { } domain && evt.ToDomainLocation() is { } location)
        {
            return state with { DomainData = state.DomainData.SetItem((location, domain), evt) };
        }

        return evt is PipelineEvent.AlertRaised raised ? state.Apply(raised) : state;
    }

    public static GrpcConsumerState Apply(this GrpcConsumerState state, PipelineEvent.AlertRaised raised)
    {
        var alerts = (state.Alerts.GetValueOrDefault(raised.Location)?.Alerts ?? [])
            .Where(alert => alert.Type != raised.Type)
            .Append(raised.Alert)
            .OrderBy(alert => alert.Type)
            .ToList();
        return state with { Alerts = state.Alerts.SetItem(raised.Location, new AlertResult(raised.Location, alerts)) };
    }

    public static PipelineEvent? GetDomainData(this GrpcConsumerState state, Location location, DomainKind domain) =>
        state.DomainData.GetValueOrDefault((location, domain));

    public static AlertResult? GetLatestAlerts(this GrpcConsumerState state, Location location) =>
        state.Alerts.GetValueOrDefault(location);
}
