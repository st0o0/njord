using Njord.Compute.Analysis;
using Njord.Domain.Sensors;
using Njord.Messages.Egress;

namespace Njord.Enrichment;

public interface IStatelessEnrichment : IEnrichmentFeature
{
    IEnumerable<EgressEvent> Compute(ConsensusSnapshot consensus, SensorSnapshot? sensors = null);
}
