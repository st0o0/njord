using Njord.Consensus;

namespace Njord.Messages.Derived;

public sealed record DerivedSettings(IReadOnlyList<int> Horizons);
