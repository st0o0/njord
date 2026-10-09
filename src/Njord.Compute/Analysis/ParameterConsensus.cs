using Newtonsoft.Json;
using Njord.Weather;

namespace Njord.Compute.Analysis;

public sealed record ParameterConsensus(
    [property: JsonProperty("parameter")] ParameterDef Parameter,
    [property: JsonProperty("byHorizon")] IReadOnlyDictionary<string, HorizonConsensus> ByHorizon);
