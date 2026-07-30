using Njord.Domain.Analysis;
using Njord.Egress;

namespace Njord.Enrichment;

public interface IStatelessEnrichment : IEnrichmentFeature
{
    IEnumerable<EgressEvent> Compute(ConsensusSnapshot consensus);
}
