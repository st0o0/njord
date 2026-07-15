using Njord.Domain.Weather;
using Njord.Egress;

namespace Njord.Enrichment;

public interface IStatefulEnrichment : IEnrichmentFeature
{
    IEnumerable<EgressEvent> Compute(ModelSnapshot snapshot, ModelSnapshot? previous, IReadOnlyList<string> locations);
}
