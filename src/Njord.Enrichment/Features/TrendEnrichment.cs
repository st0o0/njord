using Microsoft.Extensions.Options;
using Njord.Configuration;
using Njord.Domain.Analysis;
using Njord.Domain.Sensors;
using Njord.Egress;
using Njord.Messages.Egress;

namespace Njord.Enrichment.Features;

internal sealed class TrendEnrichment : IStatefulEnrichment
{
    private readonly TrendComputer _computer;
    private readonly bool _enabled;

    public string TypeName => EnrichmentTypeNames.Trends;
    public bool Enabled => _enabled;

    public TrendEnrichment(
        IOptions<NjordOptions> options,
        TrendComputer computer)
    {
        _computer = computer;
        _enabled = options.Value.Enrichment.IsEnabled(TypeName);
    }

    public IEnumerable<EgressEvent> Compute(
        ConsensusSnapshot consensus, ConsensusSnapshot? previous, SensorSnapshot? sensors = null)
    {
        if (previous is null)
        {
            yield break;
        }

        var result = _computer.Compute(consensus, previous);
        yield return new EgressEvent.EnrichmentUpdate(consensus.Location, TypeName, result);
    }
}
