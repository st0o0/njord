namespace Njord.Compute.Contracts;

public sealed record DecayInfo(
    double DecayRate,
    int? ReliableHours);
