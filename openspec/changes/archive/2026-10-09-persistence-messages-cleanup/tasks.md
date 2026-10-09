## 1. Persistence: convert DTOs to sealed records

- [ ] 1.1 Convert `src/Njord.Persistence/BudgetTrackerDtos.cs` DTOs to sealed records with `{ get; init; }`, remove `Version` property. Split into file-per-type under `Pipeline/` subdirectory.
- [ ] 1.2 Convert `src/Njord.Persistence/SchedulerDtos.cs` DTOs the same way, under `Pipeline/`.
- [ ] 1.3 Convert `src/Njord.Persistence/ForecastHistoryDtos.cs` DTOs, under `Enrichment/`.
- [ ] 1.4 Convert `src/Njord.Persistence/ForecastSnapshotDtos.cs` DTOs, under `Grpc/`.
- [ ] 1.5 Convert `src/Njord.Persistence/EnrichmentSnapshotDtos.cs` DTOs, under `Grpc/`.
- [ ] 1.6 Delete original multi-type files after all splits are done.
- [ ] 1.7 Update all `*DtoMapping.cs` files in domain projects to work with the new record constructors (adapt `FromPersistence`/`ToDto` methods).
- [ ] 1.8 Run `Njord.Persistence.Tests` — confirm Verify roundtrip snapshots pass (re-approve if JSON shape changed due to Version field removal).

## 2. Persistence: move DataChanged out of actor

- [ ] 2.1 Move the `DataChanged` record from `src/Njord.Pipeline/SchedulerActor.cs` to `src/Njord.Persistence/Pipeline/DataChangedDto.cs` (it's a persistence event).
- [ ] 2.2 Update `SchedulerActor` and `SchedulerDtoMapping` to reference the new location.
- [ ] 2.3 Confirm build and Pipeline tests pass.

## 3. Messages: reunite split definitions

- [ ] 3.1 Move `QueryHistoryResponse`/`QueryHistoryResult`/`QueryHistoryFailed` from `src/Njord.Enrichment/ForecastHistoryMessages.cs` into `src/Njord.Messages/Enrichment/EnrichmentMessages.cs`, co-located with the `QueryHistory` query.
- [ ] 3.2 Update all `using` statements and call sites referencing the moved types.
- [ ] 3.3 Move `SubscribeInbound` from `src/Njord.Mqtt/MqttConnectionActor.cs` to `src/Njord.Messages/Mqtt/MqttMessages.cs`.

## 4. Messages: standardize response naming

- [ ] 4.1 Rename `ForecastFound` → `QueryForecastCompleted` and `ForecastNotAvailable` → `QueryForecastFailed` in `src/Njord.Messages/Snapshots/SnapshotMessages.cs`. Update all call sites.
- [ ] 4.2 Rename `SensorSnapshotFound` → `QuerySensorSnapshotCompleted` and `SensorSnapshotNotFound` → `QuerySensorSnapshotFailed`. Update all call sites.
- [ ] 4.3 Add abstract base `TriggerImmediatePollResponse` with `TriggerImmediatePollCompleted`/`TriggerImmediatePollFailed` subtypes (renaming existing `TriggerImmediatePollResult`).
- [ ] 4.4 Rename remaining `*Result` response types to `*Completed` where they're the success variant of a query (e.g. `QueryPollStatesResult` → `QueryPollStatesCompleted`).
- [ ] 4.5 Build and fix all compiler errors from the renames.

## 5. Messages: file organization

- [ ] 5.1 Split `SchedulerMessages.cs` (9 types) into individual files under `src/Njord.Messages/Pipeline/`.
- [ ] 5.2 Split `SnapshotMessages.cs` (16 types) into individual files under `src/Njord.Messages/Snapshots/`.
- [ ] 5.3 Split `BudgetMessages.cs` (4 types) into individual files under `src/Njord.Messages/Pipeline/`.

## Validation

- [ ] 6.1 `dotnet build Njord.slnx` (run from `src/`)
- [ ] 6.2 `dotnet run --project Njord.Persistence.Tests/Njord.Persistence.Tests.csproj --no-build`
- [ ] 6.3 Run all test projects sequentially (bash loop from AGENTS.md).
- [ ] 6.4 `dotnet slopwatch analyze -d . --fail-on warning` (run from repo root)
