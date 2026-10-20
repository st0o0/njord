using System.Collections.Immutable;
using Njord.Forecast;
using Njord.Grpc.Mapping;
using Njord.Messages.Alerts;
using Njord.Messages.Pipeline;

namespace Njord.Grpc;

internal sealed record GrpcConsumerState(
    ImmutableDictionary<(Location Location, DomainKind Domain), PipelineEvent> DomainData,
    ImmutableDictionary<Location, AlertResult> Alerts,
    ImmutableDictionary<(Location Location, LifestyleKind Kind), LifestyleUpdate> Lifestyle)
{
    public static readonly GrpcConsumerState Empty = new(
        ImmutableDictionary<(Location, DomainKind), PipelineEvent>.Empty,
        ImmutableDictionary<Location, AlertResult>.Empty,
        ImmutableDictionary<(Location, LifestyleKind), LifestyleUpdate>.Empty);
}

internal static class GrpcConsumerStateExtensions
{
    public static GrpcConsumerState Apply(this GrpcConsumerState state, PipelineEvent evt)
    {
        if (evt.ToDomainKind() is { } domain && evt.ToDomainLocation() is { } location)
        {
            return state with { DomainData = state.DomainData.SetItem((location, domain), evt) };
        }

        if (evt is PipelineEvent.AlertRaised raised)
        {
            return state.Apply(raised);
        }

        return evt.ToLifestyleUpdate() is { } lifestyle
            ? state with { Lifestyle = state.Lifestyle.SetItem((lifestyle.Location, lifestyle.Kind), lifestyle) }
            : state;
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

    public static IReadOnlyList<LifestyleUpdate> GetLatestLifestyle(this GrpcConsumerState state, Location location) =>
        state.Lifestyle
            .Where(entry => entry.Key.Location == location)
            .OrderBy(entry => entry.Key.Kind)
            .Select(entry => entry.Value)
            .ToList();

    public static AlertResult? GetLatestAlerts(this GrpcConsumerState state, Location location) =>
        state.Alerts.GetValueOrDefault(location);
}
