using Njord.Consensus;
using Njord.Consensus.Settings;
using Njord.Messages.Alerts;
using Njord.Messages.Derived;
using Njord.Messages.Indices;
using Njord.Messages.Trends;

namespace Njord.Messages.Config;

public sealed record AnalysisSettings(
    ConsensusSettings Consensus,
    AlertSettings Alerts,
    IndexSettings Indices,
    TrendSettings Trends,
    DerivedSettings Derived);
