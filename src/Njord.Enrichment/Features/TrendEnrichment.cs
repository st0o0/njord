using Microsoft.Extensions.Options;
using Njord.Compute.Analysis;
using Njord.Core.Configuration;
using Njord.Core.Enrichment;
using Njord.Domain.Sensors;
using Njord.Messages.Egress;

namespace Njord.Enrichment.Features;

internal sealed class TrendEnrichment : IStatefulEnrichment
{
    private readonly TrendComputer _computer;

    public string TypeName => EnrichmentTypeNames.Trends;
    public bool Enabled { get; }

    public TrendEnrichment(
        IOptions<EnrichmentOptions> enrichmentOptions,
        TrendComputer computer)
    {
        _computer = computer;
        Enabled = enrichmentOptions.Value.IsEnabled(TypeName);
    }

    public IEnumerable<EgressEvent> Compute(ConsensusSnapshot consensus, ConsensusSnapshot? previous)
    {
        if (previous is null)
        {
            yield break;
        }

        var result = _computer.Compute(consensus, previous);
        yield return new EgressEvent.EnrichmentUpdate(consensus.Location, TypeName, result);
    }
}
