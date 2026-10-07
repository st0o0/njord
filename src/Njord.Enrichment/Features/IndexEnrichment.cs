using Microsoft.Extensions.Options;
using Njord.Compute.Analysis;
using Njord.Core.Configuration;
using Njord.Core.Enrichment;
using Njord.Domain.Sensors;
using Njord.Messages.Egress;

namespace Njord.Enrichment.Features;

internal sealed class IndexEnrichment : IStatelessEnrichment
{
    private readonly IndexComputer _indexComputer;
    private readonly IReadOnlyDictionary<(string Location, string Score), ResolvedPreferences> _resolvedPreferences;
    private readonly bool _enabled;

    public string TypeName => EnrichmentTypeNames.Indices;
    public bool Enabled => _enabled;

    public IndexEnrichment(
        IOptions<NjordOptions> options,
        IOptions<EnrichmentOptions> enrichmentOptions,
        IndexComputer indexComputer)
    {
        _indexComputer = indexComputer;
        _enabled = enrichmentOptions.Value.IsEnabled(TypeName);
        var locationNames = options.Value.Locations.Select(l => l.Name);
        _resolvedPreferences = PreferenceResolver.Resolve(enrichmentOptions.Value.Indices, locationNames);
    }

    public IEnumerable<EgressEvent> Compute(ConsensusSnapshot consensus, SensorSnapshot? sensors = null)
    {
        var prefs = sensors?.Get(SensorKind.IndoorTemperature) is { } liveTemp
            ? OverrideIndoorTemp(_resolvedPreferences, consensus.Location, liveTemp)
            : _resolvedPreferences;

        var result = _indexComputer.Compute(consensus, prefs);
        yield return new EgressEvent.EnrichmentUpdate(consensus.Location, TypeName, result);
    }

    private static IReadOnlyDictionary<(string Location, string Score), ResolvedPreferences> OverrideIndoorTemp(
        IReadOnlyDictionary<(string Location, string Score), ResolvedPreferences> source,
        string location,
        double indoorTemp)
    {
        var result = new Dictionary<(string, string), ResolvedPreferences>(source);
        foreach (var key in source.Keys.Where(k => k.Location == location))
        {
            result[key] = source[key] with { IndoorTemp = indoorTemp };
        }

        return result;
    }

}
