using Njord.Core.Domain;
using Njord.Messages.Pipeline;

namespace Njord.Pipeline;

// The analysis state of the pipeline: the latest LocationForecast per location, the previous value of every domain
// slot, and the projection groups that react to a changed domain. Lives inside the StatefulSelectMany closure.
internal sealed class ProjectionEngine(IReadOnlyList<IProjectionGroup> groups, TimeProvider time)
{
    private readonly Dictionary<string, (LocationForecast Current, LocationForecast? Previous)> _locations =
        new(StringComparer.OrdinalIgnoreCase);

    public IReadOnlyList<PipelineEvent> Process(DomainDataReceived received)
    {
        var (current, previous) = Update(received);
        var events = new List<PipelineEvent>(4) { received.ToPipelineEvent() };

        foreach (var group in groups)
        {
            if (!group.AffectedBy(received.Domain))
            {
                continue;
            }

            foreach (var enrichmentEvent in group.Evaluate(current, previous, time))
            {
                events.Add(enrichmentEvent.ToPipelineEvent());
            }
        }

        return events;
    }

    private (LocationForecast Current, LocationForecast? Previous) Update(DomainDataReceived received)
    {
        _locations.TryGetValue(received.Location.Name, out var state);
        var current = state.Current ?? new LocationForecast(received.Location);

        var previous = state.Previous;
        if (current.Get(received.Domain) is { } replaced)
        {
            previous = (previous ?? new LocationForecast(received.Location)).With(received.Domain, replaced);
        }

        current = current.With(received.Domain, received.Data);
        _locations[received.Location.Name] = (current, previous);
        return (current, previous);
    }
}
