using Microsoft.Extensions.Options;
using Njord.Consensus;
using Njord.Indices.Configuration;
using Njord.Messages.Indices;
using Njord.Model;
using Njord.Sensors;

namespace Njord.Indices;

public sealed class IndexAnalyzer
{
    private readonly IReadOnlyList<IIndexScore> _scores;
    private readonly TimeProvider _timeProvider;

    public IndexAnalyzer(IEnumerable<IIndexScore> scores, IOptions<IndexOptions> options, TimeProvider timeProvider)
    {
        _scores = OrderedAndComplete(scores);
        _timeProvider = timeProvider;
        Enabled = options.Value.Enabled;
    }

    public bool Enabled { get; }

    public IndexResult? Compute(LocationConsensus consensus, SensorReadings? sensors, IndexSettings settings)
    {
        if (!Enabled)
        {
            return null;
        }

        var effective = sensors?.Get(SensorKind.IndoorTemperature) is { } liveTemp
            ? new IndexSettings(OverrideIndoorTemp(settings.Preferences, consensus.Location, liveTemp))
            : settings;

        return Analyze(consensus, effective);
    }

    private IndexResult Analyze(LocationConsensus consensus, IndexSettings settings)
    {
        var slices = DaySlicer.Slice(consensus, _timeProvider.GetUtcNow());
        var days = slices.Select(slice => ScoreDay(slice, consensus.Location, settings)).ToList();

        var today = slices.Count > 0 ? slices[0].FullDayMeans : new Dictionary<ParameterDef, double?>();
        var vpd = VaporPressureDeficit.Categorize(
            today.GetValueOrDefault(ParameterRegistry.Temperature2m),
            today.GetValueOrDefault(ParameterRegistry.RelativeHumidity2m));

        return new IndexResult(consensus.Location, days, FrostProtection.Compute(consensus), vpd);
    }

    private DayScoreSet ScoreDay(DaySlice slice, LocationId location, IndexSettings settings)
    {
        var scored = _scores.ToDictionary(score => score.Kind, score => score.Score(slice, PreferencesFor(score.Kind)));

        return new DayScoreSet(slice.DayOffset, scored, slice.HoursIncluded);

        ResolvedPreferences PreferencesFor(IndexKind kind) =>
            settings.Preferences.TryGetValue((location, kind), out var preferences) ? preferences : ResolvedPreferences.Default;
    }

    private static IReadOnlyList<IIndexScore> OrderedAndComplete(IEnumerable<IIndexScore> scores)
    {
        var ordered = scores.OrderBy(score => score.Kind).ToList();
        var counts = ordered.CountBy(score => score.Kind).ToDictionary();
        var missing = Enum.GetValues<IndexKind>().Where(kind => !counts.ContainsKey(kind)).ToList();
        var duplicated = counts.Where(c => c.Value > 1).Select(c => c.Key).ToList();

        if (missing.Count > 0 || duplicated.Count > 0)
        {
            throw new InvalidOperationException(
                "Every index kind needs exactly one IIndexScore. " +
                $"Missing: [{string.Join(", ", missing)}]; duplicated: [{string.Join(", ", duplicated)}].");
        }

        return ordered;
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
