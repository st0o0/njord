using Njord.Core;

namespace Njord.Messages.Results.Trends;

public enum ConfidenceTrendDirection
{
    Converging,
    Stable,
    Diverging,
}

public enum PollenType
{
    Birch,
    Grass,
    Alder,
    Mugwort,
}

public enum PollenTrendDirection
{
    Stable,
    Rising,
    Falling,
}

public sealed record ConfidenceTrendResult(Location Location, ConfidenceTrendDirection Direction, double Magnitude, double CurrentAvgStdDev, double PreviousAvgStdDev) : TrendFeatureResult(Location);

public sealed record PollenTrendResult(
    Location Location,
    PollenType? PeakType,
    DateTimeOffset? PeakTime,
    PollenTrendDirection? Birch,
    PollenTrendDirection? Grass,
    PollenTrendDirection? Alder,
    PollenTrendDirection? Mugwort) : TrendFeatureResult(Location);
