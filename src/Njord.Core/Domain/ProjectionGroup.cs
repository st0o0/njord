namespace Njord.Core.Domain;

public sealed record ProjectionGroup<TInput>(
    IProjector<TInput> Projector,
    IReadOnlyList<IEnrichment<TInput>> Enrichments)
    where TInput : class, IEnrichmentInput
{
    public IReadOnlyList<EnrichmentEvent> Evaluate(
        LocationForecast current,
        LocationForecast? previous,
        TimeProvider time)
    {
        var input = Projector.Project(current, previous);
        if (input is null)
        {
            return [];
        }

        var events = new List<EnrichmentEvent>();
        foreach (var enrichment in Enrichments)
        {
            events.AddRange(enrichment.Evaluate(input, time));
        }

        return events;
    }
}
