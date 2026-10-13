using Njord.Weather;

namespace Njord.Compute.Consensus;

public sealed record ConsensusSnapshot(
    LocationId Location,
    HourlyConsensus Hourly,
    DailyConsensus Daily,
    DateTimeOffset ComputedAt);
