using Njord.Model;

namespace Njord.Core.Domain;

public interface IProjector<out TInput> where TInput : IEnrichmentInput
{
    IReadOnlySet<DomainKind> RequiredDomains { get; }

    TInput? Project(LocationForecast current, LocationForecast? previous);
}
