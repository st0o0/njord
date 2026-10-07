using Newtonsoft.Json;

namespace Njord.Compute.Analysis;

public sealed record HorizonDerived(
    [property: JsonProperty("beaufort")] int? Beaufort,
    [property: JsonProperty("windChill")] double? WindChill,
    [property: JsonProperty("dewPointComfort")] string? DewPointComfort,
    [property: JsonProperty("wmoDescription")] string? WmoDescription);
