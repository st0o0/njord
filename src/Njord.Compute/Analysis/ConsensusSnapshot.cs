using Njord.Weather;

namespace Njord.Compute.Analysis;

public sealed record ConsensusSnapshot(
    LocationId Location,
    HourlyConsensus Hourly,
    DailyConsensus Daily,
    DateTimeOffset ComputedAt);
