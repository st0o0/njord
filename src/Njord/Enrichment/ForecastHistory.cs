using Njord.Domain;

namespace Njord.Enrichment;

public sealed record ForecastRecord(
    DateTimeOffset Timestamp,
    string Location,
    IReadOnlyDictionary<WeatherModel, IReadOnlyDictionary<string, double?>> ModelValues,
    IReadOnlyDictionary<string, double?> ConsensusValues);

public sealed class ForecastHistory
{
    private readonly List<ForecastRecord> _records = [];
    private readonly int _retentionDays;

    public IReadOnlyList<ForecastRecord> Records => _records;

    public ForecastHistory(int retentionDays = 30) => _retentionDays = retentionDays;

    public void Add(ForecastRecord record)
    {
        _records.Add(record);
        Prune(record.Timestamp);
    }

    public IReadOnlyList<ForecastRecord> Query(DateTimeOffset now)
    {
        var cutoff = now.AddDays(-_retentionDays);
        return _records.Where(r => r.Timestamp >= cutoff).ToList();
    }

    private void Prune(DateTimeOffset now)
    {
        var cutoff = now.AddDays(-_retentionDays);
        _records.RemoveAll(r => r.Timestamp < cutoff);
    }
}

public sealed record ForecastRecorded(
    DateTimeOffset Timestamp,
    string Location,
    IReadOnlyDictionary<WeatherModel, IReadOnlyDictionary<string, double?>> ModelValues,
    IReadOnlyDictionary<string, double?> ConsensusValues);

public sealed record RecordSnapshot(ModelSnapshot Snapshot);

public sealed record QueryHistory;

public sealed record HistoryResponse(ForecastHistory History);
