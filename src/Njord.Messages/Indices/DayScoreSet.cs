namespace Njord.Messages.Indices;

public sealed record DayScoreSet(
    int DayOffset,
    IReadOnlyDictionary<IndexKind, ScoredIndex> Scores,
    int HoursIncluded);
