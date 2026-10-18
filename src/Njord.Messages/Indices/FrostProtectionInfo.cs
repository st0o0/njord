namespace Njord.Messages.Indices;

public sealed record FrostProtectionInfo(
    int HoursUntilFrost,
    double Confidence);
