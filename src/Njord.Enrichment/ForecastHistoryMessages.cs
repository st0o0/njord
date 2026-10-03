using Njord.Domain.Analysis;
using Njord.Domain.Weather;

namespace Njord.Enrichment;

public sealed record RecordSnapshot(ModelSnapshot Snapshot);

public sealed record QueryHistory;

public abstract record QueryHistoryResponse;
public sealed record QueryHistoryResult(ForecastHistory History) : QueryHistoryResponse;
public sealed record QueryHistoryFailed(Exception Cause) : QueryHistoryResponse;
