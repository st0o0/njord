namespace Njord.Messages.Alerts;

public enum ConfidenceLevel
{
    Low,
    Medium,
    High,
}

public sealed record ConfidenceThresholds(double HighBelowStdDev, double LowAboveStdDev)
{
    public static readonly ConfidenceThresholds Default = new(1.0, 3.0);

    public ConfidenceLevel LevelFor(double? stdDev) => stdDev switch
    {
        null => ConfidenceLevel.Medium,
        var s when s < HighBelowStdDev => ConfidenceLevel.High,
        var s when s > LowAboveStdDev => ConfidenceLevel.Low,
        _ => ConfidenceLevel.Medium,
    };
}

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
