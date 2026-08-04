using Newtonsoft.Json;

namespace Njord.Domain.Analysis;

public sealed record ExtremaTimingInfo(
    [property: JsonProperty("maxInHours")] int? MaxInHours,
    [property: JsonProperty("minInHours")] int? MinInHours);
