using Newtonsoft.Json;

namespace Njord.Analysis;

public sealed record PrecipTimingInfo(
    [property: JsonProperty("startsInHours")] int? StartsInHours,
    [property: JsonProperty("endsInHours")] int? EndsInHours);
