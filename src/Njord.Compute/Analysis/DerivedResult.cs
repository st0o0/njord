using Njord.Weather;

namespace Njord.Compute.Analysis;

public sealed record DerivedResult(
    LocationId Location,
    IReadOnlyDictionary<Horizon, HorizonDerived> ByHorizon,
    ScalarDerived Scalars);
