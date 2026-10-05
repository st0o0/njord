using Njord.Analysis;
using Njord.Domain.Sensors;
using Njord.Messages.Egress;

namespace Njord.Enrichment;

public interface IStatefulEnrichment : IEnrichmentFeature
{
    IEnumerable<EgressEvent> Compute(ConsensusSnapshot consensus, ConsensusSnapshot? previous, SensorSnapshot? sensors = null);
}
