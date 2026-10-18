using Njord.Consensus;

namespace Njord.Messages.Trends;

// Trend computation has no tunable parameters yet; the record keeps the per-computation shape uniform.
public sealed record TrendSettings
{
    public static TrendSettings Default { get; } = new();
}
