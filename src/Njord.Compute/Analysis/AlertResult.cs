using Newtonsoft.Json;
using Njord.Weather;

namespace Njord.Compute.Analysis;

public sealed record AlertResult(
    [property: JsonProperty("location")] LocationId Location,
    [property: JsonProperty("alerts")] IReadOnlyList<Alert> Alerts);
