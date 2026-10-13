using Microsoft.Extensions.Options;
using Njord.Compute.Analysis;
using Njord.Compute.Settings;
using Njord.Core.Configuration.Indices;
using Njord.Sensors;
using Njord.Weather;

namespace Njord.Indices;

public sealed class IndexAnalyzer
{
    private readonly IndexComputer _indexComputer;

    public IndexAnalyzer(IndexComputer indexComputer, IOptions<IndexOptions> options)
    {
        _indexComputer = indexComputer;
        Enabled = options.Value.Enabled;
    }

    public bool Enabled { get; }

    public IndexResult? Compute(ConsensusSnapshot consensus, SensorSnapshot? sensors, IndexSettings settings)
    {
        if (!Enabled)
            return null;

        var effective = sensors?.Get(SensorKind.IndoorTemperature) is { } liveTemp
            ? new IndexSettings(OverrideIndoorTemp(settings.Preferences, consensus.Location, liveTemp))
            : settings;

        return _indexComputer.Compute(consensus, effective);
    }

    private static IReadOnlyDictionary<(LocationId Location, IndexKind Kind), ResolvedPreferences> OverrideIndoorTemp(
        IReadOnlyDictionary<(LocationId Location, IndexKind Kind), ResolvedPreferences> source,
        LocationId location,
        double indoorTemp)
    {
        var result = new Dictionary<(LocationId, IndexKind), ResolvedPreferences>(source);
        foreach (var key in source.Keys.Where(k => k.Location == location))
        {
            result[key] = source[key] with { IndoorTemp = indoorTemp };
        }

        return result;
    }
}
