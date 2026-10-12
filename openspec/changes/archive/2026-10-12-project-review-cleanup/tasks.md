## 1. Post-refactor cleanup

- [x] 1.1 Delete ghost directories `src/Njord.Ingest/` and `src/Njord.Ingest.Tests/`
- [x] 1.2 ~~Update `Akka.Persistence.Sql.Hosting` from 1.5.70 to 1.5.73~~ — 1.5.70 IS the latest (different release cadence); no change needed
- [x] 1.3 Build solution: `dotnet build Njord.slnx`

## 2. Persistence FunkArr alignment — renames

- [x] 2.1 Rename `BudgetTrackerSnapshotDto` → `PersistedBudgetTrackerState` in `src/Njord.Persistence/BudgetTrackerDtos.cs`, keep all `[JsonProperty]` strings unchanged
- [x] 2.2 Rename `ApiCallRecordedDto` → `ApiCallRecorded` in `src/Njord.Persistence/BudgetTrackerDtos.cs`, keep all `[JsonProperty]` strings unchanged
- [x] 2.3 Rename `DataChangedDto` → `DataChanged` in `src/Njord.Persistence/SchedulerDtos.cs`, keep all `[JsonProperty]` strings unchanged
- [x] 2.4 Update all references in `src/Njord.Budget/BudgetTracker.cs`, `src/Njord.Budget/BudgetTrackerState.cs`, and `src/Njord.Budget/BudgetTrackerDtoMapping.cs`
- [x] 2.5 Update all references in `src/Njord.Pipeline/PollScheduler.cs`, `src/Njord.Pipeline/SchedulerState.cs`, and `src/Njord.Pipeline/SchedulerDtoMapping.cs`
- [x] 2.6 Update all references in `src/Njord.Persistence.Tests/`
- [x] 2.7 Update all references in `src/Njord.Budget.Tests/`
- [x] 2.8 Build and run affected tests: `dotnet build Njord.slnx && dotnet run --project Njord.Persistence.Tests/Njord.Persistence.Tests.csproj --no-build && dotnet run --project Njord.Budget.Tests/Njord.Budget.Tests.csproj --no-build && dotnet run --project Njord.Pipeline.Tests/Njord.Pipeline.Tests.csproj --no-build`

## 3. Persistence FunkArr alignment — project layout

- [x] 3.1 Create `src/Njord.Persistence/Events/Budget/` and move budget persistence records there
- [x] 3.2 Create `src/Njord.Persistence/Events/Pipeline/` and move pipeline persistence records there
- [x] 3.3 Update namespaces to `Njord.Persistence.Events.Budget` and `Njord.Persistence.Events.Pipeline`
- [x] 3.4 Update all `using` statements across consumers and tests
- [x] 3.5 Rename files to match FunkArr convention: `BudgetTrackerDtos.cs` → split into `PersistedBudgetTrackerState.cs` + `ApiCallRecorded.cs`; `SchedulerDtos.cs` → `DataChanged.cs`
- [x] 3.6 Build and run all tests: `dotnet build Njord.slnx && dotnet run --project Njord.Persistence.Tests/Njord.Persistence.Tests.csproj --no-build && dotnet run --project Njord.Budget.Tests/Njord.Budget.Tests.csproj --no-build && dotnet run --project Njord.Pipeline.Tests/Njord.Pipeline.Tests.csproj --no-build`

## 4. Persistence tests

- [x] 4.1 Add `DataChanged` roundtrip test in `src/Njord.Persistence.Tests/` (serialize → deserialize → assert all fields match)
- [x] 4.2 Add wire-format snapshot test for `PersistedBudgetTrackerState` in `src/Njord.Persistence.Tests/`
- [x] 4.3 Add wire-format snapshot test for `ApiCallRecorded` in `src/Njord.Persistence.Tests/`
- [x] 4.4 Add wire-format snapshot test for `DataChanged` in `src/Njord.Persistence.Tests/`
- [x] 4.5 Run and approve baselines: `dotnet run --project Njord.Persistence.Tests/Njord.Persistence.Tests.csproj`

## 5. Pipeline zone architecture test

- [x] 5.1 Add `Njord.Pipeline` to `NjordArchitecture.cs` as a separate constant (not in `FeatureLibraryNames`)
- [x] 5.2 Add dedicated zone test in `src/Njord.Architecture.Tests/ZoneArchitectureSpec.cs` that asserts Pipeline depends on no feature library except Alerts, Derived, Trends, Indices
- [x] 5.3 Run architecture tests: `dotnet run --project Njord.Architecture.Tests/Njord.Architecture.Tests.csproj --no-build`

## 6. Metrics naming and dead code

- [x] 6.1 Rename `njord_fetch_total` → `njord_openmeteo_fetch_total` in `src/Njord.OpenMeteo/Telemetry.cs`
- [x] 6.2 Rename `njord_fetch_duration_seconds` → `njord_openmeteo_fetch_duration_seconds` in `src/Njord.OpenMeteo/Telemetry.cs`
- [x] 6.3 Rename `njord_data_changed_total` → `njord_pipeline_data_changed_total` in `src/Njord.Pipeline/Telemetry.cs`
- [x] 6.4 Remove dead `ThrottleWait` histogram from `src/Njord.Pipeline/Telemetry.cs`
- [x] 6.5 Update any test assertions referencing old metric names
- [x] 6.6 Build: `dotnet build Njord.slnx`

## 7. Test assertion convention fixes

- [x] 7.1 Fix `Assert.True(offenders.Count == 0, ...)` → `Assert.Empty(offenders)` in `src/Njord.Architecture.Tests/DisabledTestArchitectureSpec.cs:23`
- [x] 7.2 ~~Fix `Assert.True(a == b)` → `Assert.Equal(a, b)`~~ — intentionally tests `==` operator, not `Equals()`; correct as-is
- [x] 7.3 Fix 2× vacuous `Assert.NotNull` after `Ask<Ack>` in `src/Njord.Pipeline.Tests/PollSchedulerSpec.cs:70,80` — removed
- [x] 7.4 Fix `Assert.Single` then `[0]` → capture pattern in `src/Njord.Core.Tests/BudgetCalculatorSpec.cs:103`
- [x] 7.5 Fix `Assert.Single` then `[0]` → capture pattern in `src/Njord.Mqtt.Tests/StatePayloadBuilderSpec.cs:237`
- [x] 7.6 Clean up straightforward `!.` on nullable results (target files: `SensorSnapshotSpec.cs`, `ResolvedParameterSetLookupSpec.cs`, `ModelCoverageRegistrySpec.cs`, `IndexResultSpec.cs`, `ConsensusComputerSpec.cs`)
- [x] 7.7 Update `!.` known-deviation count in `AGENTS.md` (21 → 4)
- [x] 7.8 Run affected test suites — all green

## 8. Test coverage gaps

- [x] 8.1 Add `IndexAnalyzer` test with sensor snapshot containing indoor temperature reading in `src/Njord.Indices.Tests/`
- [x] 8.2 Add `IndexAnalyzer` test verifying sensor override does not persist across calls in `src/Njord.Indices.Tests/`
- [x] 8.3 Run Indices tests: `dotnet run --project Njord.Indices.Tests/Njord.Indices.Tests.csproj --no-build`
- [x] 8.4 Identify the flaky `Njord.Core.Tests` test — `ValidatePersistenceWritable` file race across parallel tests
- [x] 8.5 Stabilize flaky test — unique temp paths per test invocation in 3 validator spec files

## 9. Validation

- [x] 9.1 Full build: `dotnet build Njord.slnx` — 0 errors, 0 warnings
- [x] 9.2 Run all test projects sequentially — 665 tests, 0 failures
- [x] 9.3 Run architecture tests and verify Pipeline zone coverage — 34 tests, all green
