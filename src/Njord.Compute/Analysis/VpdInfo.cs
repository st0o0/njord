using Newtonsoft.Json;

namespace Njord.Compute.Analysis;

public sealed record VpdInfo(
    [property: JsonProperty("category")] string Category,
    [property: JsonProperty("vpd")] double Vpd);
