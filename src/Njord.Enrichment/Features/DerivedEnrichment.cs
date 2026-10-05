using Microsoft.Extensions.Options;
using Njord.Analysis;
using Njord.Configuration;
using Njord.Domain.Sensors;
using Njord.Domain.Weather;
using Njord.Egress;
using Njord.Messages.Egress;

namespace Njord.Enrichment.Features;

internal sealed class DerivedEnrichment : IStatelessEnrichment
{
    private readonly DerivedResultComputer _computer;
    private readonly IReadOnlyList<int> _horizons;
    private readonly bool _enabled;

    public string TypeName => EnrichmentTypeNames.Derived;
    public bool Enabled => _enabled;

    public DerivedEnrichment(
        IOptions<NjordOptions> options,
        IOptions<EnrichmentOptions> enrichmentOptions,
        DerivedResultComputer computer)
    {
        _computer = computer;
        _horizons = [.. options.Value.Horizons];
        _enabled = enrichmentOptions.Value.IsEnabled(TypeName);
    }

    public IEnumerable<EgressEvent> Compute(ConsensusSnapshot consensus, SensorSnapshot? sensors = null)
    {
        var result = _computer.Compute(consensus, _horizons);
        yield return new EgressEvent.EnrichmentUpdate(consensus.Location, TypeName, result);
    }
}
