## 1. Rename Pipeline message responses

- [x] 1.1 Renamed BudgetResponse → QueryBudgetUsageResponse, BudgetUsageResult → QueryBudgetUsageResult, BudgetResponseFailed → QueryBudgetUsageFailed
- [x] 1.2 Renamed SchedulerQueryResponse → QueryPollStatesResponse, PollStatesResult → QueryPollStatesResult, SchedulerQueryFailed → QueryPollStatesFailed
- [x] 1.3 Renamed TriggerPollResult → TriggerImmediatePollResult
- [x] 1.4 Updated all consumers in Pipeline, Pipeline.Tests, Grpc, Grpc.Tests (15 files)

## 2. Rename Sensor message responses

- [x] 2.1 Renamed SensorSnapshotQueryResponse → QuerySensorSnapshotResponse, SensorSnapshotQueryFailed → QuerySensorSnapshotFailed
- [x] 2.2 Updated consumers in Sensors, Enrichment (3 files)

## 3. Rename Snapshot message responses

- [x] 3.1 Renamed ForecastQueryResponse → QueryForecastResponse, AllForecastsQueryResponse → QueryAllForecastsResponse, EnrichmentQueryResponse → QueryEnrichmentResponse, AllEnrichmentsQueryResponse → QueryAllEnrichmentsResponse (and subtypes)
- [x] 3.2 Updated consumers in Grpc, Grpc.Tests (9 files)

## 4. Rename Enrichment message responses

- [x] 4.1 Renamed HistoryQueryResponse → QueryHistoryResponse, ForecastHistoryResult → QueryHistoryResult, HistoryQueryFailed → QueryHistoryFailed
- [x] 4.2 Updated consumers in Enrichment, Njord.Tests (5 files)

## 5. Validation

- [x] 5.1 Build: 0 errors, 0 warnings
- [x] 5.2 All tests pass
- [x] 5.3 Slopwatch: 0 issues
- [x] 5.4 dotnet format: clean
