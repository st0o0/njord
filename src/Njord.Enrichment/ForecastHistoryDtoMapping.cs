using Njord.Domain.Analysis;
using Njord.Domain.Weather;

namespace Njord.Persistence;

public static class ForecastHistoryDtoMapping
{
    public static ForecastRecordDto ToDto(ForecastRecord record) => new()
    {
        TimestampUtcTicks = record.Timestamp.UtcTicks,
        Location = record.Location,
        ModelValues = record.ModelValues.ToDictionary(
            kvp => kvp.Key.Id,
            kvp => new Dictionary<string, double?>(kvp.Value)),
        ConsensusValues = new Dictionary<string, double?>(record.ConsensusValues),
    };

    public static ForecastRecord ToDomain(ForecastRecordDto dto)
    {
        var modelValues = dto.ModelValues.ToDictionary(
            kvp => new WeatherModel(kvp.Key),
            kvp => (IReadOnlyDictionary<string, double?>)new Dictionary<string, double?>(kvp.Value));
        var consensus = new Dictionary<string, double?>(dto.ConsensusValues);
        return new ForecastRecord(
            new DateTimeOffset(dto.TimestampUtcTicks, TimeSpan.Zero),
            dto.Location,
            modelValues,
            consensus);
    }

    public static ForecastHistorySnapshotDto ToDto(ForecastHistory history) => new()
    {
        RetentionDays = history.RetentionDays,
        Records = history.Records.Select(ToDto).ToList(),
    };

    public static ForecastHistory ToDomain(ForecastHistorySnapshotDto dto)
    {
        var history = new ForecastHistory(dto.RetentionDays);
        foreach (var record in dto.Records)
            history.Add(ToDomain(record));
        return history;
    }
}
