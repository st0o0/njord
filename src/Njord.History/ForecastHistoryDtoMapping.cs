using Njord.Compute.Analysis;
using Njord.Persistence;
using Njord.Weather;

namespace Njord.History;

public static class ForecastHistoryDtoMapping
{
    public static PersistedForecastRecord ToDto(ForecastRecord record) => new()
    {
        TimestampUtcTicks = record.Timestamp.UtcTicks,
        Location = record.Location,
        ModelValues = record.ModelValues.ToDictionary(
            kvp => kvp.Key.Id,
            kvp => new Dictionary<string, double?>(kvp.Value)),
        ConsensusValues = new Dictionary<string, double?>(record.ConsensusValues),
    };

    public static ForecastRecord ToDomain(PersistedForecastRecord dto)
    {
        var modelValues = dto.ModelValues.ToDictionary(
            kvp => new WeatherModel(kvp.Key), IReadOnlyDictionary<string, double?> (kvp) => new Dictionary<string, double?>(kvp.Value));
        var consensus = new Dictionary<string, double?>(dto.ConsensusValues);
        return new ForecastRecord(
            new DateTimeOffset(dto.TimestampUtcTicks, TimeSpan.Zero),
            dto.Location,
            modelValues,
            consensus);
    }

    public static PersistedForecastHistory ToDto(ForecastHistory history) => new()
    {
        RetentionDays = history.RetentionDays,
        Records = [.. history.Records.Select(ToDto)],
    };

    public static ForecastHistory ToDomain(PersistedForecastHistory dto)
    {
        var history = new ForecastHistory(dto.RetentionDays);
        foreach (var record in dto.Records)
        {
            history.Add(ToDomain(record));
        }

        return history;
    }
}
