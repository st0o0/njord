## 1. Rename Pipeline message responses

- [ ] 1.1 In `src/Njord.Messages/Pipeline/BudgetMessages.cs`: rename `BudgetResponse` → `QueryBudgetUsageResponse`, `BudgetUsageResult` → `QueryBudgetUsageResult`, `BudgetResponseFailed` → `QueryBudgetUsageFailed`
- [ ] 1.2 In `src/Njord.Messages/Pipeline/SchedulerMessages.cs`: rename `SchedulerQueryResponse` → `QueryPollStatesResponse`, `PollStatesResult` → `QueryPollStatesResult`, `SchedulerQueryFailed` → `QueryPollStatesFailed`
- [ ] 1.3 Rename `TriggerPollResult` → `TriggerImmediatePollResult` (or add `TriggerImmediatePollResponse` base if appropriate)
- [ ] 1.4 Update all consumers in `src/Njord.Pipeline/` and `src/Njord.Pipeline.Tests/`

## 2. Rename Sensor message responses

- [ ] 2.1 In `src/Njord.Messages/Sensors/SensorHubMessages.cs`: rename `SensorSnapshotQueryResponse` → `QuerySensorSnapshotResponse`, update subtypes (`SensorSnapshotFound`, `SensorSnapshotQueryFailed` → `QuerySensorSnapshotFailed`)
- [ ] 2.2 Update all consumers in `src/Njord.Sensors/`, `src/Njord.Enrichment/`, `src/Njord.Tests/`

## 3. Rename Snapshot message responses

- [ ] 3.1 In `src/Njord.Messages/Snapshots/SnapshotMessages.cs`: rename `ForecastQueryResponse` → `QueryForecastResponse` (and subtypes), `AllForecastsQueryResponse` → `QueryAllForecastsResponse` (and subtypes), `EnrichmentQueryResponse` → `QueryEnrichmentResponse` (and subtypes), `AllEnrichmentsQueryResponse` → `QueryAllEnrichmentsResponse` (and subtypes)
- [ ] 3.2 Update all consumers in `src/Njord.Grpc/`, `src/Njord.Grpc.Tests/`, `src/Njord.Tests/`

## 4. Rename Enrichment message responses

- [ ] 4.1 In `src/Njord.Enrichment/ForecastHistoryMessages.cs`: rename `HistoryQueryResponse` → `QueryHistoryResponse`, `ForecastHistoryResult` → `QueryHistoryResult`, `HistoryQueryFailed` → `QueryHistoryFailed`
- [ ] 4.2 Update all consumers in `src/Njord.Enrichment/`, `src/Njord.Tests/`

## 5. Validation

- [ ] 5.1 Build solution: `dotnet build src/Njord.slnx`
- [ ] 5.2 Run all test suites:
  ```
  for p in src/Njord.*Tests; do
    [ "$p" = src/Njord.Tests.Shared ] && continue
    dotnet run --project "$p/$(basename $p).csproj" --no-build
  done
  ```
- [ ] 5.3 Run slopwatch: `dotnet tool restore && dotnet slopwatch analyze -d . --fail-on warning`
- [ ] 5.4 Run dotnet format: `dotnet format src/Njord.slnx --verify-no-changes`
