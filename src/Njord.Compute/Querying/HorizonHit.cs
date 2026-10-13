using Njord.Compute.Consensus;
using Njord.Compute.Contracts;
using Njord.Weather;

namespace Njord.Compute.Querying;

public sealed record HorizonHit(double Value, Horizon Horizon, double? Agreement, int ModelCount);
