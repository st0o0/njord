namespace Njord.Analysis;

public sealed record ConsensusSnapshot(
    string Location,
    HourlyConsensus Hourly,
    DailyConsensus Daily,
    DateTimeOffset ComputedAt);
