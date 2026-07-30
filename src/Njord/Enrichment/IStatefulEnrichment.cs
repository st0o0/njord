using Njord.Domain.Analysis;
using Njord.Egress;

namespace Njord.Enrichment;

public interface IStatefulEnrichment : IEnrichmentFeature
{
    IEnumerable<EgressEvent> Compute(ConsensusSnapshot consensus, ConsensusSnapshot? previous);
}
