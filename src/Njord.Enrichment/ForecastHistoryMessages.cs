using Njord.Compute.Analysis;

namespace Njord.Enrichment;

public abstract record QueryHistoryResponse;
public sealed record QueryHistoryResult(ForecastHistory History) : QueryHistoryResponse;
public sealed record QueryHistoryFailed(Exception Cause) : QueryHistoryResponse;
