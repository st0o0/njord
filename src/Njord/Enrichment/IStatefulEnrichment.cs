using Njord.Domain.Analysis;
using Njord.Domain.Sensors;
using Njord.Egress;

namespace Njord.Enrichment;

public interface IStatefulEnrichment : IEnrichmentFeature
{
    IEnumerable<EgressEvent> Compute(ConsensusSnapshot consensus, ConsensusSnapshot? previous, SensorSnapshot? sensors = null);
}
