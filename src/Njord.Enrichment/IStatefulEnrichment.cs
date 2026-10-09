using Njord.Compute.Analysis;
using Njord.Messages.Egress;
using Njord.Sensors;

namespace Njord.Enrichment;

public interface IStatefulEnrichment : IEnrichmentFeature
{
    IEnumerable<EgressEvent> Compute(ConsensusSnapshot consensus, ConsensusSnapshot? previous);
}
