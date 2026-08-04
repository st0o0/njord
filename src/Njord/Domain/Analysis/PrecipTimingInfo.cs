using Newtonsoft.Json;

namespace Njord.Domain.Analysis;

public sealed record PrecipTimingInfo(
    [property: JsonProperty("startsInHours")] int? StartsInHours,
    [property: JsonProperty("endsInHours")] int? EndsInHours);
