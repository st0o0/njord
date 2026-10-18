using Njord.Weather;

namespace Njord.Consensus;

public sealed record LocationConsensus(
    LocationId Location,
    HourlyConsensus Hourly,
    DailyConsensus Daily,
    DateTimeOffset ComputedAt);
