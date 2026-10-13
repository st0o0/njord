using Njord.Weather;

namespace Njord.Compute.Analysis;

public sealed record IndexResult(
    LocationId Location,
    IReadOnlyList<DayScoreSet> Days,
    FrostProtectionInfo? FrostProtection,
    VpdInfo? Vpd);
