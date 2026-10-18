using Njord.Weather;

namespace Njord.Messages.Derived;

public sealed record DerivedResult(
    LocationId Location,
    IReadOnlyDictionary<Horizon, HorizonDerived> ByHorizon,
    ScalarDerived Scalars);
