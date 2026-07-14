using Njord.Domain.Analysis;
using Njord.Domain.Weather;

namespace Njord.Enrichment;

public sealed record RecordSnapshot(ModelSnapshot Snapshot);

public sealed record QueryHistory;

public sealed record HistoryResponse(ForecastHistory History);
