using Njord.Weather;

namespace Njord.Consensus;

public sealed record ParameterConsensus(
    ParameterDef Parameter,
    IReadOnlyDictionary<Horizon, HorizonConsensus> ByHorizon);
