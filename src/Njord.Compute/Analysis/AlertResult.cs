using Newtonsoft.Json;

namespace Njord.Compute.Analysis;

public sealed record AlertResult(
    [property: JsonProperty("location")] string Location,
    [property: JsonProperty("alerts")] IReadOnlyList<Alert> Alerts);
