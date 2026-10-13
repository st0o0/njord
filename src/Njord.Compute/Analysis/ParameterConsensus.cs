using Njord.Weather;

namespace Njord.Compute.Analysis;

public sealed record ParameterConsensus(
    ParameterDef Parameter,
    IReadOnlyDictionary<Horizon, HorizonConsensus> ByHorizon);
