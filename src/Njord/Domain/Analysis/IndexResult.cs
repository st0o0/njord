using Newtonsoft.Json;

namespace Njord.Domain.Analysis;

public sealed record IndexResult(
    [property: JsonProperty("location")] string Location,
    [property: JsonProperty("days")] IReadOnlyList<DayScoreSet> Days,
    [property: JsonProperty("frostProtection")] FrostProtectionInfo? FrostProtection,
    [property: JsonProperty("vpd")] VpdInfo? Vpd);
