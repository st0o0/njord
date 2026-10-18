using Njord.Consensus;
using Njord.Messages.Alerts;
using Njord.Messages.Derived;
using Njord.Messages.Indices;
using Njord.Messages.Pipeline;
using Njord.Messages.Trends;
using Njord.Weather;

namespace Njord.Tests.Shared;

// Minimal enrichment events, one per analysis kind, for specs that care about routing rather than content.
public static class EnrichmentEvents
{
    public static PipelineEvent.ConsensusComputed Consensus(LocationId location, DateTimeOffset at) =>
        new(new LocationConsensus(location, new HourlyConsensus([], 72), new DailyConsensus([], 4), at), at);

    public static PipelineEvent.AlertComputed Alerts(LocationId location, DateTimeOffset at) =>
        new(new AlertResult(location, []), at);

    public static PipelineEvent.DerivedComputed Derived(LocationId location, DateTimeOffset at) =>
        new(new DerivedResult(location, new Dictionary<Horizon, HorizonDerived>(), new ScalarDerived(4.5, null, null)), at);

    public static PipelineEvent.TrendComputed Trends(LocationId location, DateTimeOffset at) =>
        new(new TrendResult(location, new Dictionary<string, ParameterTrend?>(), null,
            new PrecipTimingInfo(3, null), new ExtremaTimingInfo(null, null), null, null), at);

    public static PipelineEvent.IndexComputed Indices(LocationId location, DateTimeOffset at) =>
        new(new IndexResult(location, [], new FrostProtectionInfo(14, 0.75), null), at);
}
