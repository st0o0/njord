using Njord.Compute.Consensus;
using Njord.Compute.Contracts;
namespace Njord.Compute.Settings;

public sealed record DerivedSettings(IReadOnlyList<int> Horizons);
