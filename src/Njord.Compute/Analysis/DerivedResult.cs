using Newtonsoft.Json;
using Njord.Weather;

namespace Njord.Compute.Analysis;

public sealed record DerivedResult(
    [property: JsonProperty("location")] LocationId Location,
    [property: JsonProperty("byHorizon")] IReadOnlyDictionary<Horizon, HorizonDerived> ByHorizon,
    [property: JsonProperty("scalars")] ScalarDerived Scalars);
