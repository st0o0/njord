using Njord.Model;

namespace Njord.Consensus.Querying;

public sealed record HorizonHit(double Value, Horizon Horizon, double? Agreement, int ModelCount);
