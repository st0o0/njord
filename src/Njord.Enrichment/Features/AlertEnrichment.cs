using Microsoft.Extensions.Options;
using Njord.Analysis;
using Njord.Configuration;
using Njord.Domain.Sensors;
using Njord.Egress;
using Njord.Messages.Egress;

namespace Njord.Enrichment.Features;

internal sealed class AlertEnrichment : IStatelessEnrichment
{
    private readonly AlertOptions _alertOptions;
    private readonly TimeProvider _timeProvider;
    private readonly bool _enabled;

    public string TypeName => EnrichmentTypeNames.Alerts;
    public bool Enabled => _enabled;

    public AlertEnrichment(
        IOptions<NjordOptions> options,
        TimeProvider timeProvider)
    {
        _alertOptions = options.Value.Enrichment.Alerts;
        _timeProvider = timeProvider;
        _enabled = options.Value.Enrichment.IsEnabled(TypeName);
    }

    public IEnumerable<EgressEvent> Compute(ConsensusSnapshot consensus, SensorSnapshot? sensors = null)
    {
        var result = AlertEvaluator.EvaluateAll(consensus, _alertOptions, _timeProvider);
        yield return new EgressEvent.EnrichmentUpdate(consensus.Location, TypeName, result);
    }
}
