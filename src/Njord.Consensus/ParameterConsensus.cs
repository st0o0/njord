using Njord.Model;

namespace Njord.Consensus;

public sealed record ParameterConsensus(
    ParameterDef Parameter,
    IReadOnlyDictionary<Horizon, HorizonConsensus> ByHorizon);
