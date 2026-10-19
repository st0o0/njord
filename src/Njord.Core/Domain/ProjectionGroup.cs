using Njord.Model;

namespace Njord.Core.Domain;

public sealed record ProjectionGroup<TInput>(
    IProjector<TInput> Projector,
    IReadOnlyList<IEnrichment<TInput>> Enrichments)
    : IProjectionGroup
    where TInput : class, IEnrichmentInput
{
    public IReadOnlySet<DomainKind> RequiredDomains => Projector.RequiredDomains;

    public bool AffectedBy(DomainKind domain) => Projector.RequiredDomains.Contains(domain);

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
