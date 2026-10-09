using Newtonsoft.Json;

namespace Njord.Persistence;

public sealed record ModelForecastDto
{
    [JsonProperty("model")] public string ModelId { get; init; } = "";
    [JsonProperty("loc")] public string Location { get; init; } = "";
    [JsonProperty("cycle")] public long CycleUtcTicks { get; init; }
    [JsonProperty("hourly")] public ForecastPointDto[] Hourly { get; init; } = [];
    [JsonProperty("daily")] public DailyForecastPointDto[] Daily { get; init; } = [];
}
