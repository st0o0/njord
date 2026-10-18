namespace Njord.Messages.Trends;

public sealed record DecayInfo(
    double DecayRate,
    int? ReliableHours);
