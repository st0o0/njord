using System.Collections.Immutable;
using Njord.Domain.Weather;
using Njord.Persistence;

namespace Njord.Grpc;

public sealed record ForecastSnapshotState(
    ImmutableDictionary<string, ModelForecast> Forecasts,
    int UpdatesSinceSnapshot)
{
    public static readonly ForecastSnapshotState Empty = new(
        ImmutableDictionary<string, ModelForecast>.Empty, 0);
}

public static class ForecastSnapshotStateExtensions
{
    public static ForecastSnapshotState Apply(this ForecastSnapshotState state, string key, ModelForecast forecast) =>
        state with
        {
            Forecasts = state.Forecasts.SetItem(key, forecast),
            UpdatesSinceSnapshot = state.UpdatesSinceSnapshot + 1,
        };

    public static ForecastSnapshotState ResetSnapshotCounter(this ForecastSnapshotState state) =>
        state with { UpdatesSinceSnapshot = 0 };

    public static ForecastQueryResponse GetForecast(this ForecastSnapshotState state, string key) =>
        state.Forecasts.TryGetValue(key, out var forecast)
            ? new ForecastFound(forecast)
            : new ForecastNotFound(key);

    public static AllForecastsResult GetAllForecasts(this ForecastSnapshotState state) =>
        new(state.Forecasts.ToDictionary(
            kvp => ParseKey(kvp.Key),
            kvp => kvp.Value));

    public static ForecastSnapshotDto GetPersistenceState(this ForecastSnapshotState state) =>
        ForecastSnapshotMapping.ToDto(new Dictionary<string, ModelForecast>(state.Forecasts));

    public static ForecastSnapshotState FromPersistence(ForecastSnapshotDto dto) =>
        new(ForecastSnapshotMapping.ToDomain(dto).ToImmutableDictionary(), 0);

    public static string MakeKey(string location, string modelId) => $"{location}|{modelId}";

    private static (string Location, string ModelId) ParseKey(string key)
    {
        var sep = key.IndexOf('|');
        return (key[..sep], key[(sep + 1)..]);
    }
}
