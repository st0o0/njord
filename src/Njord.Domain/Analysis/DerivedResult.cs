using Newtonsoft.Json;

namespace Njord.Domain.Analysis;

public sealed record DerivedResult(
    [property: JsonProperty("location")] string Location,
    [property: JsonProperty("byHorizon")] IReadOnlyDictionary<string, HorizonDerived> ByHorizon,
    [property: JsonProperty("scalars")] ScalarDerived Scalars);
