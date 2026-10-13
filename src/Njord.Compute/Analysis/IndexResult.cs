using Newtonsoft.Json;
using Njord.Weather;

namespace Njord.Compute.Analysis;

public sealed record IndexResult(
    [property: JsonProperty("location")] LocationId Location,
    [property: JsonProperty("days")] IReadOnlyList<DayScoreSet> Days,
    [property: JsonProperty("frostProtection")] FrostProtectionInfo? FrostProtection,
    [property: JsonProperty("vpd")] VpdInfo? Vpd);
