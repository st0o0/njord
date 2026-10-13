using Njord.Compute.Contracts;

namespace Njord.Indices;

public interface IIndexScore
{
    IndexKind Kind { get; }

    ScoredIndex Score(DaySlice slice, ResolvedPreferences preferences);
}
