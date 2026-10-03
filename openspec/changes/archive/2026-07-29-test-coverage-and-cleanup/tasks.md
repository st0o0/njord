## 1. Move FetchOutcome to Domain

- [x] 1.1 Move `src/Njord/Ingest/FetchOutcome.cs` to `src/Njord/Domain/Weather/FetchOutcome.cs`, change namespace to `Njord.Domain.Weather`
- [x] 1.2 Update all `using Njord.Ingest` to `using Njord.Domain.Weather` where the import was only for FetchOutcome — files: `src/Njord/Egress/ModelStateActor.cs`, `src/Njord/Enrichment/EnrichmentActor.cs`, `src/Njord/Pipeline/PipelineActor.cs`, `src/Njord/Pipeline/SchedulerActor.cs`, `src/Njord/Pipeline/SchedulerMessages.cs`
- [x] 1.3 Keep FetchOutcome importable from Ingest callers (`src/Njord/Ingest/OpenMeteoClient.cs`, `src/Njord/Ingest/IOpenMeteoClient.cs`) by adding `using Njord.Domain.Weather` where needed
- [x] 1.4 Update test files referencing FetchOutcome to use the new namespace
- [x] 1.5 Verify no file under `src/Njord/Egress/` or `src/Njord/Enrichment/` contains `using Njord.Ingest`

## 2. BackoffSupervisor for persistent actors

- [x] 2.1 Add `BackoffSupervisor` wrapping for SchedulerActor, BudgetTrackerActor, ForecastSnapshotActor, EnrichmentSnapshotActor in `src/Njord/Configuration/NjordActorSystemSetup.cs` — min 3s, max 30s, random factor 0.2 (ForecastHistoryActor excluded: child actor with per-instance args)
- [x] 2.2 Existing SchedulerActor tests verify restart behavior; BackoffSupervisor Props are integration-verified by the build

## 3. Enrichment Compute() tests

- [x] 3.1 Add `src/Njord.Tests/Enrichment/Features/ConsensusEnrichmentSpec.cs` — test Compute() with multi-model snapshot, single model, empty snapshot
- [x] 3.2 Add `src/Njord.Tests/Enrichment/Features/AlertEnrichmentSpec.cs` — test Compute() with frost, heat, storm conditions and no-alert baseline
- [x] 3.3 Add `src/Njord.Tests/Enrichment/Features/DerivedEnrichmentSpec.cs` — test Compute() for derived value calculations
- [x] 3.4 Add `src/Njord.Tests/Enrichment/Features/EnergyEnrichmentSpec.cs` — test Compute() for COP and heating degree calculations
- [x] 3.5 Add `src/Njord.Tests/Enrichment/Features/IndexEnrichmentSpec.cs` — test Compute() for HDD/CDD index calculations
- [x] 3.6 Add `src/Njord.Tests/Enrichment/Features/TrendEnrichmentSpec.cs` — test Compute() with previous snapshot for trend detection

## 4. MqttEgressActor tests

- [x] 4.1 Add `src/Njord.Tests/Mqtt/MqttEgressActorSpec.cs` — test ref lifecycle (WaitingForRefs → Ready → Terminated → re-request), message mapping (PerModelUpdate → MqttMessage), and delta deduplication (same payload not re-published)

## 5. Validation

- [x] 5.1 Run full test suite: `dotnet run --project Njord.Tests/Njord.Tests.csproj` from `src/`
- [x] 5.2 Verify zero `using Njord.Ingest` in Egress and Enrichment directories via grep
