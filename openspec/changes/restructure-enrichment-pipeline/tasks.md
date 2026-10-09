## 1. Create feature library projects (with compute logic from Njord.Compute)

- [x] 1.1 Create `src/Njord.Alerts/` project with `IAlertInput`, `AlertAnalyzer`, `AlertOptions`, and `AddAlerts()` DI extension. Move `AlertEvaluator`, `Alert`, `AlertResult`, `AlertSeverity`, `AlertType` from `src/Njord.Compute/Analysis/` and alert logic from `src/Njord.Enrichment/AlertEnrichment.cs`. Project references `Njord.Core` only.
- [x] 1.2 Create `src/Njord.Alerts.Tests/` with `AlertAnalyzerSpec` — test compute logic via `IAlertInput`, verify no-result when disabled. `[Fact(Timeout = 5000)]`, sealed, BDD names.
- [x] 1.3 Create `src/Njord.Derived/` project with `IDerivedInput`, `DerivedComputer`, and `AddDerived()` DI extension. Move `DerivedComputer`, `DerivedResultComputer`, `DerivedResult` from `src/Njord.Compute/Analysis/` and logic from `src/Njord.Enrichment/DerivedEnrichment.cs`.
- [x] 1.4 Create `src/Njord.Derived.Tests/` with `DerivedComputerSpec`.
- [x] 1.5 Create `src/Njord.Trends/` project with `ITrendInput`, `TrendAnalyzer`, and `AddTrends()` DI extension. Move `TrendComputer`, `TrendAnalyzer`, `TrendResult` from `src/Njord.Compute/Analysis/` and logic from `src/Njord.Enrichment/TrendEnrichment.cs`. `TrendAnalyzer` is stateless — receives current + previous via interface.
- [x] 1.6 Create `src/Njord.Trends.Tests/` with `TrendAnalyzerSpec` — test with null previous (first cycle) and with previous snapshot.
- [x] 1.7 Create `src/Njord.Indices/` project with `IIndexInput`, `IndexComputer`, `IndexOptions`, and `AddIndices()` DI extension. Move `IndexComputer`, `IndexScorer`, `IndexResult` from `src/Njord.Compute/Analysis/` and logic from `src/Njord.Enrichment/IndexEnrichment.cs`.
- [x] 1.8 Create `src/Njord.Indices.Tests/` with `IndexComputerSpec`.
- [x] 1.9 Create `src/Njord.History/` project with `IHistoryInput`, `HistoryTracker`, `ForecastHistoryActor`, `HistoryOptions`, and `AddHistory()` DI extension. Move `HistoryComputer`, `HistoryAnalyzer`, `HistoryResult`, `ForecastHistory`, `ForecastRecord` from `src/Njord.Compute/Analysis/`, actor from `src/Njord.Enrichment/ForecastHistoryActor.cs`, and history mapping extensions into this project. Persistence IDs and DTO wire format unchanged.
- [x] 1.10 Create `src/Njord.History.Tests/` with `HistoryTrackerSpec` and `ForecastHistoryActorSpec` — test persistence recovery, retention trimming, inline analysis. Use Akka.Hosting.TestKit with in-memory persistence.
- [ ] 1.11 Clean up `src/Njord.Compute/` — remove migrated feature types, verify only `ConsensusComputer`, `ConsensusResult`, `ConsensusSnapshot`, `ConsensusSnapshotFactory`, and shared types remain.
- [ ] 1.12 Update `src/Njord.Compute.Tests/` — remove tests for migrated types, verify consensus tests still pass.

## 2. Rename EgressEvent to PipelineEvent and remove untyped messages

- [ ] 2.1 Rename `EgressEvent` → `PipelineEvent` in `src/Njord.Messages/`. Rename subtypes: `PerModelUpdate` → `ModelForecast`, `EnrichmentUpdate` → `AnalysisResult` (typed result, no `object` + `string TypeName`), `CapabilityLearned` → `ModelCapability`.
- [ ] 2.2 Remove untyped enrichment messages from `src/Njord.Messages/`: `UpdateEnrichment`, `QueryEnrichment`, `QueryEnrichmentCompleted`, `QueryAllEnrichmentsCompleted`, `IWithEnrichmentKey`.
- [ ] 2.3 Update all usages across the codebase: Egress, Mqtt, Grpc, Pipeline, tests.

## 3. Flatten Core — remove enrichment registry

- [ ] 3.1 Remove `EnrichmentTypeNames` static class from `src/Njord.Core/`.
- [ ] 3.2 Remove enrichment-specific feature-registry types: `IEnrichmentFeature`, `IStatelessEnrichment`, `IStatefulEnrichment`, `IActorEnrichment` interfaces.
- [ ] 3.3 Refactor `EnrichmentOptions` in `src/Njord.Core/` — remove `IsEnabled()` switch expression and per-feature sub-objects. Each feature library now owns its own options under `Njord:Analysis:<Feature>`.
- [ ] 3.4 Update `src/Njord.Core.Tests/` — remove tests for removed types, update options validation specs.

## 4. Restructure MQTT as SourceRef consumer — remove discovery

- [ ] 4.1 Remove `IEnrichmentPresenter` interface and all 6 presenter implementations from `src/Njord.Mqtt/`.
- [ ] 4.2 Remove `MqttDiscoveryActor`, `DiscoveryPayloadBuilder`, `DiscoveryContext`, and all discovery-related code from `src/Njord.Mqtt/`.
- [ ] 4.3 Remove discovery-related config (`DiscoveryEnabled`, birth-message handling) from MQTT options and connection actor.
- [ ] 4.4 Create `MqttConsumerActor` in `src/Njord.Mqtt/` — requests `SourceRef<PipelineEvent>` from `PipelineActor`, materializes its own sink that publishes state to MQTT. Create `AddMqttConsumer()` DI extension. Keep connection actor and LWT (`njord/status`).
- [ ] 4.5 Update `src/Njord.Mqtt.Tests/` — remove presenter specs, discovery specs, golden-master discovery snapshots. Add consumer specs for SourceRef subscription, typed result → state payload translation, `expire_after` and unavailable handling.

## 5. Restructure gRPC as SourceRef consumer with secondary BroadcastHub

- [ ] 5.1 Create `GrpcConsumerActor` in `src/Njord.Grpc/` — requests `SourceRef<PipelineEvent>` from `PipelineActor`, holds latest state in-memory (`Dictionary<Location, LatestResults>`), provides secondary `BroadcastHub<PipelineEvent>` for streaming RPCs. Apply KeepAlive + IdleTimeout for dead gRPC client cleanup.
- [ ] 5.2 Remove `GrpcSnapshotConsumerActor` (150-line SourceRef handshaker) from `src/Njord.Grpc/`.
- [ ] 5.3 Remove `EnrichmentSnapshotActor` shard region and its registration from `src/Njord.Grpc/`.
- [ ] 5.4 Update `WeatherGrpcService` — `GetEnrichments` queries `GrpcConsumerActor` for in-memory state. `StreamForecasts`/`StreamEnrichments` request SourceRef from `GrpcConsumerActor`'s secondary BroadcastHub.
- [ ] 5.5 Update `AdminService.SetEnrichment` — replace `TypeName` string-based config writes with typed `Njord:Analysis:<Feature>` paths.
- [ ] 5.6 Create `AddGrpcConsumer()` DI extension in `src/Njord.Grpc/`.
- [ ] 5.7 Update `src/Njord.Grpc.Tests/` — remove snapshot actor specs, add `GrpcConsumerActorSpec` (in-memory state, SourceRef subscription, secondary hub), adapt gRPC service specs.

## 6. Rebuild PipelineActor — full graph to BroadcastHub

- [ ] 6.1 Extend `PipelineActor` in `src/Njord.Pipeline/` to own the full graph: Fetch → Consensus → Scan (previous) → Build PollContext → Broadcast → [Feature Flows via Select/SelectAsync] → Merge → KeepAlive → Buffer → IdleTimeout → `BroadcastHub<PipelineEvent>`. Include `CapabilityTracker` inline.
- [x] 6.2 Create `PollContext` record in `src/Njord/` implementing `IAlertInput`, `ITrendInput`, `IIndexInput`, `IHistoryInput`, `IDerivedInput`. Assembled in the pipeline after consensus computation.
- [x] 6.3 Add `RequestPipelineSource` / `PipelineSourceResponse` messages for consumers to request SourceRef.
- [ ] 6.4 Update `src/Njord.Pipeline.Tests/` — test full graph materialization, feature invocation, BroadcastHub endpoint, KeepAlive behavior.

## 7. Dissolve Njord.Egress

- [ ] 7.1 Move `CapabilityTracker` logic into PipelineActor's graph (inline Select stage).
- [ ] 7.2 Delete `src/Njord.Egress/` project — `ModelStateActor` relay function absorbed into pipeline graph.
- [ ] 7.3 Delete `src/Njord.Egress.Tests/` project.
- [ ] 7.4 Remove `Njord.Egress`, `Njord.Egress.Tests`, and `EgressSetupContainer` from solution and Host.

## 8. Remove old Enrichment project

- [ ] 8.1 Delete `src/Njord.Enrichment/` project (all code migrated to feature libraries and pipeline graph).
- [ ] 8.2 Delete `src/Njord.Enrichment.Tests/` project.
- [ ] 8.3 Remove from `src/Njord.slnx`.

## 9. Persistence — FunkArr convention and cleanup

- [ ] 9.1 Rename persistence DTOs to `Persisted*` prefix: `ForecastHistorySnapshotDto` → `PersistedForecastHistory`, `ForecastRecordDto` → `PersistedForecastRecord`, etc. Update `[JsonProperty]` names if needed (extend-only: keep wire compatibility).
- [ ] 9.2 Remove `EnrichmentSnapshotDto` and `EnrichmentEntryDto` from `src/Njord.Persistence/` (no more persisted enrichment snapshots).
- [ ] 9.3 Move mapping extensions from `src/Njord.Persistence/` into the feature library that owns the actor (`Njord.History` for history mappings).
- [ ] 9.4 Update `src/Njord.Persistence.Tests/` — rename snapshot references, remove enrichment DTO specs.

## 10. Host wiring and DI consolidation

- [ ] 10.1 Consolidate all actor registrations into a single `AkkaSetupContainer` in `src/Njord/Configuration/`, organized by domain in private methods.
- [x] 10.2 Wire feature DI extensions (`AddAlerts()`, `AddDerived()`, `AddTrends()`, `AddIndices()`, `AddHistory()`) and consumer DI extensions (`AddMqttConsumer()`, `AddGrpcConsumer()`) in `src/Njord/Configuration/`.
- [ ] 10.3 Remove `EnrichmentSetupContainer`, `MqttSetupContainer`, `EgressSetupContainer` if replaced by consolidated registration.
- [ ] 10.4 Update config binding to `Njord:Analysis:<Feature>` sections.

## 11. Update architecture tests and solution

- [x] 11.1 Update `src/Njord.Architecture.Tests/ZoneArchitectureSpec.cs` — add new feature libraries to lateral independence rules, remove Enrichment and Egress.
- [x] 11.2 Update `src/Njord.Architecture.Tests/LayerReferenceSpec.cs` — feature libraries reference only Core and below, verify Compute no longer holds feature logic.
- [x] 11.3 Add all new projects to `src/Njord.slnx`, remove dissolved projects.
- [x] 11.4 Update `AGENTS.md` — new project structure, updated test count, removed enrichment/egress references, updated pipeline description.

## 12. Validation

- [ ] 12.1 Build: `dotnet build Njord.slnx` from `src/`
- [ ] 12.2 Run all test projects (sequential loop from `src/`):
```bash
for p in Njord.*Tests; do
  [ "$p" = Njord.Tests.Shared ] && continue
  dotnet run --project "$p/$p.csproj" --no-build
done
```
- [ ] 12.3 Run architecture tests: `dotnet run --project Njord.Architecture.Tests/Njord.Architecture.Tests.csproj`
- [ ] 12.4 Verify history persistence continuity: `ForecastHistoryActor` recovers from existing DTOs in `Njord.History.Tests`
- [ ] 12.5 Verify gRPC streaming: `GrpcConsumerActor` secondary BroadcastHub serves SourceRefs to streaming RPCs
- [ ] 12.6 Run OpenSpec validation: `openspec validate --all --no-interactive`
