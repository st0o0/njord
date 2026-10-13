using Njord.Compute.Consensus;
using Njord.Compute.Contracts;
namespace Njord.Compute.Settings;

public sealed record AnalysisSettings(
    ConsensusSettings Consensus,
    AlertSettings Alerts,
    IndexSettings Indices,
    TrendSettings Trends,
    DerivedSettings Derived);
