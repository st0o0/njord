using System.Collections.Immutable;
using Njord.Core.Domain;
using Njord.Messages.Pipeline;

namespace Njord.Pipeline;

internal sealed record LocationSlot(LocationForecast Current, LocationForecast? Previous);

// The analysis state of the pipeline: the latest LocationForecast per location, the previous value of every domain
// slot, and the projection groups that react to a changed domain. Lives inside the StatefulSelectMany closure.
internal sealed class ProjectionEngine(IReadOnlyList<IProjectionGroup> groups, TimeProvider time)
{
    public static readonly ImmutableDictionary<string, LocationSlot> EmptyState =
        ImmutableDictionary.Create<string, LocationSlot>(StringComparer.OrdinalIgnoreCase);

    private ImmutableDictionary<string, LocationSlot> _locations = EmptyState;

    public IReadOnlyList<PipelineEvent> Process(DomainDataReceived received)
    {
        var (next, events) = Evaluate(_locations, groups, received, time);
        _locations = next;
        return events;
    }

    public static (ImmutableDictionary<string, LocationSlot> State, IReadOnlyList<PipelineEvent> Events) Evaluate(
        ImmutableDictionary<string, LocationSlot> state,
        IReadOnlyList<IProjectionGroup> groups,
        DomainDataReceived received,
        TimeProvider time)
    {
        var slot = Update(state.GetValueOrDefault(received.Location.Name), received);
        var events = new List<PipelineEvent>(4) { received.ToPipelineEvent() };

        foreach (var group in groups)
        {
            if (!group.AffectedBy(received.Domain))
            {
                continue;
            }

            foreach (var enrichmentEvent in group.Evaluate(slot.Current, slot.Previous, time))
            {
                events.Add(enrichmentEvent.ToPipelineEvent());
            }
        }

        return (state.SetItem(received.Location.Name, slot), events);
    }

    private static LocationSlot Update(LocationSlot? slot, DomainDataReceived received)
    {
        var current = slot?.Current ?? new LocationForecast(received.Location);

        var previous = slot?.Previous;
        if (current.Get(received.Domain) is { } replaced)
        {
            previous = (previous ?? new LocationForecast(received.Location)).With(received.Domain, replaced);
        }

        return new LocationSlot(current.With(received.Domain, received.Data), previous);
    }
}
