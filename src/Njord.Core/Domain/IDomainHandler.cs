using Njord.Model;

namespace Njord.Core.Domain;

public interface IProjectionGroup
{
    IReadOnlySet<DomainKind> RequiredDomains { get; }

    bool AffectedBy(DomainKind domain);

    IReadOnlyList<EnrichmentEvent> Evaluate(LocationForecast current, LocationForecast? previous, TimeProvider time);
}
