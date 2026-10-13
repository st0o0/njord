using Microsoft.Extensions.Options;
using Njord.Compute.Analysis;
using Njord.Compute.Settings;
using Njord.Core.Configuration.Indices;
using Njord.Core.Configuration;
using Njord.Sensors;
using Njord.Weather;

namespace Njord.Indices;

public sealed class IndexAnalyzer
{
    private readonly IndexComputer _indexComputer;
    private readonly IndexSettings _settings;

    public IndexAnalyzer(
        IndexComputer indexComputer,
        IOptions<IndexOptions> indexOptions,
        IOptions<NjordOptions> njordOptions)
    {
        _indexComputer = indexComputer;
        Enabled = indexOptions.Value.Enabled;
        _settings = indexOptions.Value.ToSettings(njordOptions.Value.Locations.Select(l => l.Name));
    }

    public bool Enabled { get; }

    public IndexResult? Compute(ConsensusSnapshot consensus, SensorSnapshot? sensors)
    {
        if (!Enabled)
            return null;

        var settings = sensors?.Get(SensorKind.IndoorTemperature) is { } liveTemp
            ? new IndexSettings(OverrideIndoorTemp(_settings.Preferences, consensus.Location, liveTemp))
            : _settings;

        return _indexComputer.Compute(consensus, settings);
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
