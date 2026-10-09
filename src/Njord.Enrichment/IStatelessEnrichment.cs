using Njord.Compute.Analysis;
using Njord.Messages.Egress;
using Njord.Sensors;

namespace Njord.Enrichment;

public interface IStatelessEnrichment : IEnrichmentFeature
{
    IEnumerable<EgressEvent> Compute(ConsensusSnapshot consensus, SensorSnapshot? sensors = null);
}
