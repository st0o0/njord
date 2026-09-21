using Njord.Domain.Analysis;
using Njord.Persistence;

namespace Njord.Enrichment;

public sealed record ForecastHistoryState(
    ForecastHistory History,
    int EventsSinceSnapshot,
    int SnapshotInterval)
{
    public static ForecastHistoryState Create(int retentionDays, int snapshotInterval) =>
        new(new ForecastHistory(retentionDays), 0, snapshotInterval);
}

public static class ForecastHistoryStateExtensions
{
    public static ForecastHistoryState Apply(this ForecastHistoryState state, ForecastRecord evt)
    {
        state.History.Add(evt);
        return state with { EventsSinceSnapshot = state.EventsSinceSnapshot + 1 };
    }

    public static ForecastHistoryState ApplyRecover(
        this ForecastHistoryState state, ForecastRecord evt, DateTimeOffset cutoff)
    {
        if (evt.Timestamp < cutoff)
            return state;

        state.History.Add(evt);
        return state;
    }

    public static ForecastHistoryState ResetSnapshotCounter(this ForecastHistoryState state) =>
        state with { EventsSinceSnapshot = 0 };

    public static ForecastHistoryResult GetSnapshot(this ForecastHistoryState state) =>
        new(state.History);

    public static ForecastHistorySnapshotDto GetPersistenceState(this ForecastHistoryState state) =>
        ForecastHistoryDtoMapping.ToDto(state.History);

    public static ForecastHistoryState FromPersistence(ForecastHistorySnapshotDto dto, int snapshotInterval)
    {
        var history = ForecastHistoryDtoMapping.ToDomain(dto);
        return new ForecastHistoryState(history, 0, snapshotInterval);
    }
}
