using Njord.Domain.Weather;
using Njord.Egress;

namespace Njord.Enrichment;

public interface IStatelessEnrichment : IEnrichmentFeature
{
    IEnumerable<EgressEvent> Compute(ModelSnapshot snapshot, IReadOnlyList<string> locations);
}
