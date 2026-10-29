namespace Njord.Messages.Alerts;

public static class ConfidenceLevelExtensions
{
    public static double ToConfidence(this ConfidenceLevel level) => level switch
    {
        ConfidenceLevel.Low => 0.25,
        ConfidenceLevel.Medium => 0.5,
        ConfidenceLevel.High => 1.0,
        _ => throw new ArgumentOutOfRangeException(nameof(level), level, null),
    };
}
