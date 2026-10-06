using Newtonsoft.Json;

namespace Njord.Analysis;

public sealed record ScalarDerived(
    [property: JsonProperty("diurnalAmplitude")] double? DiurnalAmplitude,
    [property: JsonProperty("sunshinePct")] double? SunshinePct,
    [property: JsonProperty("inversion")] bool? Inversion);
