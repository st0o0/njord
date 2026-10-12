using Microsoft.Extensions.Options;
using Njord.Compute.Analysis;
using Njord.Compute.Configuration;
using Njord.Core.Configuration;
using Njord.Sensors;

namespace Njord.Indices;

public sealed class IndexAnalyzer
{
    private readonly IndexComputer _indexComputer;
    private readonly IReadOnlyDictionary<(string Location, string Score), ResolvedPreferences> _resolvedPreferences;

    public IndexAnalyzer(
        IndexComputer indexComputer,
        IOptions<IndexOptions> indexOptions,
        IOptions<NjordOptions> njordOptions)
    {
        _indexComputer = indexComputer;
        Enabled = indexOptions.Value.Enabled;
        _resolvedPreferences = PreferenceResolver.Resolve(
            indexOptions.Value, njordOptions.Value.Locations.Select(l => l.Name));
    }

    public bool Enabled { get; }

    public IndexResult? Compute(ConsensusSnapshot consensus, SensorSnapshot? sensors)
    {
        if (!Enabled)
            return null;

        var prefs = sensors?.Get(SensorKind.IndoorTemperature) is { } liveTemp
            ? OverrideIndoorTemp(_resolvedPreferences, consensus.Location, liveTemp)
            : _resolvedPreferences;

        return _indexComputer.Compute(consensus, prefs);
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
