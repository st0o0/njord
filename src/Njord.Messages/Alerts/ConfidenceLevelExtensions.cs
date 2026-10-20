namespace Njord.Messages.Alerts;

public static class ConfidenceLevelExtensions
{
    public static string ToWireName(this ConfidenceLevel level) => level switch
    {
        ConfidenceLevel.Low => "low",
        ConfidenceLevel.Medium => "medium",
        ConfidenceLevel.High => "high",
        _ => throw new ArgumentOutOfRangeException(nameof(level), level, null),
    };

    public static double ToConfidence(this ConfidenceLevel level) => level switch
    {
        ConfidenceLevel.Low => 0.25,
        ConfidenceLevel.Medium => 0.5,
        ConfidenceLevel.High => 1.0,
        _ => throw new ArgumentOutOfRangeException(nameof(level), level, null),
    };
}
