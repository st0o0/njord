using Njord.Model;

namespace Njord.Messages.Indices;

public sealed record IndexResult(
    LocationId Location,
    IReadOnlyList<DayScoreSet> Days,
    FrostProtectionInfo? FrostProtection,
    VpdInfo? Vpd);
