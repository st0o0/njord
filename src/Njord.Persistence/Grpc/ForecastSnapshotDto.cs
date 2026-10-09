using Newtonsoft.Json;

namespace Njord.Persistence;

public sealed record ForecastSnapshotDto
{
    [JsonProperty("forecasts")] public Dictionary<string, ModelForecastDto> Forecasts { get; init; } = new();
}
