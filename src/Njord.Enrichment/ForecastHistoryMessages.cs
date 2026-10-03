using Njord.Domain.Analysis;
using Njord.Domain.Weather;

namespace Njord.Enrichment;

public sealed record RecordSnapshot(ModelSnapshot Snapshot);

public sealed record QueryHistory;

public abstract record HistoryQueryResponse;
public sealed record ForecastHistoryResult(ForecastHistory History) : HistoryQueryResponse;
public sealed record HistoryQueryFailed(Exception Cause) : HistoryQueryResponse;
