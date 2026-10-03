using Newtonsoft.Json;

namespace Njord.Domain.Analysis;

public sealed record ScalarDerived(
    [property: JsonProperty("diurnalAmplitude")] double? DiurnalAmplitude,
    [property: JsonProperty("sunshinePct")] double? SunshinePct,
    [property: JsonProperty("inversion")] bool? Inversion);
