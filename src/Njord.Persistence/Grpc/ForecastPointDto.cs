using Newtonsoft.Json;

namespace Njord.Persistence;

public sealed record ForecastPointDto
{
    [JsonProperty("at")] public long ValidAtUtcTicks { get; init; }
    [JsonProperty("vals")] public Dictionary<string, double?> Values { get; init; } = new();
}
