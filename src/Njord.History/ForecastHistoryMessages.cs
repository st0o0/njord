using Njord.Compute.Analysis;

namespace Njord.History;

public abstract record QueryHistoryResponse;
public sealed record QueryHistoryCompleted(ForecastHistory History) : QueryHistoryResponse;
public sealed record QueryHistoryFailed(Exception Cause) : QueryHistoryResponse;
