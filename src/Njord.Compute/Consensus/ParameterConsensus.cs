using Njord.Weather;

namespace Njord.Compute.Consensus;

public sealed record ParameterConsensus(
    ParameterDef Parameter,
    IReadOnlyDictionary<Horizon, HorizonConsensus> ByHorizon);
