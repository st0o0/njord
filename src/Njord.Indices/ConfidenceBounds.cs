namespace Njord.Indices;

public sealed record ConfidenceBounds(PeriodMeans Pessimistic, PeriodMeans Optimistic, double Agreement);
