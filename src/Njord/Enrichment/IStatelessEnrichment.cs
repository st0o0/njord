using Njord.Domain.Analysis;
using Njord.Domain.Sensors;
using Njord.Egress;

namespace Njord.Enrichment;

public interface IStatelessEnrichment : IEnrichmentFeature
{
    IEnumerable<EgressEvent> Compute(ConsensusSnapshot consensus, SensorSnapshot? sensors = null);
}
