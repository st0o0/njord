using Newtonsoft.Json;

namespace Njord.Domain.Analysis;

public sealed record WeatherChangeResult(
    [property: JsonProperty("fromCategory")] string FromCategory,
    [property: JsonProperty("toCategory")] string ToCategory,
    [property: JsonProperty("description")] string Description);
