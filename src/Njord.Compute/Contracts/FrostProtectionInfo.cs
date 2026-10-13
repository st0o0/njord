namespace Njord.Compute.Contracts;

public sealed record FrostProtectionInfo(
    int HoursUntilFrost,
    double Confidence);
