using Njord.Compute.Analysis;
using Njord.Persistence;
using Njord.Weather;

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

    public static ForecastRecord ProcessSnapshot(
        this ForecastHistoryState state,
        ModelSnapshot snapshot,
        string location,
        ResolvedParameterSet parameters,
        DateTimeOffset now)
    {
        var consensusValues = new Dictionary<string, double?>();
        var modelValuesList = new List<IReadOnlyDictionary<string, double?>>();

        foreach (var (key, forecast) in snapshot.Entries)
        {
            if (key.Location != location)
            {
                continue;
            }

            var values = new Dictionary<string, double?>();
            var nearestPoint = forecast.Hourly.Points
                .OrderBy(p => Math.Abs((p.ValidAt - now).TotalMinutes))
                .FirstOrDefault();

            if (nearestPoint is not null)
            {
                foreach (var param in parameters.Hourly)
                {
                    values[param.ApiName] = nearestPoint.Get(param);
                }
            }

            modelValuesList.Add(values);
        }

        if (modelValuesList.Count > 0)
        {
            foreach (var param in parameters.Hourly)
            {
                var vals = modelValuesList
                    .Select(v => v.GetValueOrDefault(param.ApiName))
                    .ToList();
                consensusValues[param.ApiName] = ConsensusComputer.ComputeMedian(vals);
            }
        }

        var emptyModelValues = new Dictionary<WeatherModel, IReadOnlyDictionary<string, double?>>();
        var evt = new ForecastRecord(now, location, emptyModelValues, consensusValues);
        return evt;
    }

    public static ForecastHistoryState ApplyRecover(
        this ForecastHistoryState state, ForecastRecord evt, DateTimeOffset cutoff)
    {
        if (evt.Timestamp < cutoff)
        {
            return state;
        }

        state.History.Add(evt);
        return state;
    }

    public static ForecastHistoryState ResetSnapshotCounter(this ForecastHistoryState state) =>
        state with { EventsSinceSnapshot = 0 };

    public static QueryHistoryCompleted GetSnapshot(this ForecastHistoryState state) =>
        new(state.History);

    public static ForecastHistorySnapshotDto GetPersistenceState(this ForecastHistoryState state) =>
        ForecastHistoryDtoMapping.ToDto(state.History);

    public static ForecastHistoryState FromPersistence(ForecastHistorySnapshotDto dto, int snapshotInterval)
    {
        var history = ForecastHistoryDtoMapping.ToDomain(dto);
        return new ForecastHistoryState(history, 0, snapshotInterval);
    }
}
