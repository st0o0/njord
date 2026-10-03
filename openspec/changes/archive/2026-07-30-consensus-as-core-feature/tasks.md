## 1. ConsensusSnapshot Domain Type

- [x] 1.1 Create `ConsensusSnapshot`, `HourlyConsensus`, `DailyConsensus` records in `src/Njord/Domain/Analysis/ConsensusSnapshot.cs`. Move `Compute`, `ComputeHourly`, `ComputeDaily`, `ComputeCutoffHour`, `ComputeDailyCutoff`, `ComputeHorizon`, and `FilterByModelCount` from `ConsensusResult` and `ConsensusEnrichment` into `ConsensusSnapshot.Compute` factory method. Keep `HorizonConsensus`, `ParameterConsensus`, `OutlierInfo`, `ConfidenceIntervalInfo` unchanged in `ConsensusResult.cs` (rename file to reflect it holds shared types).
- [x] 1.2 Create `src/Njord.Tests/Domain/Analysis/ConsensusSnapshotSpec.cs` — test `Compute` factory: hourly + daily facets populated, cutoff computation, min-2-models filtering, empty snapshot for missing location. Port relevant tests from `ConsensusResultSpec.cs` and `ConsensusEnrichmentSpec.cs`.
- [x] 1.3 Delete `DailyConsensusSummary.cs` from `src/Njord/Domain/Analysis/` and `DailyConsensusSummarySpec.cs` from `src/Njord.Tests/Domain/Analysis/`.

## 2. Enrichment Interface Change

- [x] 2.1 Change `IStatelessEnrichment.Compute` signature in `src/Njord/Enrichment/IStatelessEnrichment.cs` from `Compute(ModelSnapshot snapshot, IReadOnlyList<string> locations)` to `Compute(ConsensusSnapshot consensus)`.
- [x] 2.2 Change `IStatefulEnrichment.Compute` signature in `src/Njord/Enrichment/IStatefulEnrichment.cs` from `Compute(ModelSnapshot snapshot, ModelSnapshot? previous, IReadOnlyList<string> locations)` to `Compute(ConsensusSnapshot consensus, ConsensusSnapshot? previous)`.
- [x] 2.3 Update `src/Njord.Tests/Enrichment/EnrichmentFeatureContractSpec.cs` to verify 6 features (not 7), consensus not in registry, and new interface signatures.

## 3. Refactor Enrichments to ConsensusSnapshot

- [x] 3.1 Refactor `AlertEnrichment` (`src/Njord/Enrichment/Features/AlertEnrichment.cs`) and `AlertEvaluator` (`src/Njord/Domain/Analysis/AlertEvaluator.cs`) to accept `ConsensusSnapshot`. Alerts evaluate consensus medians against thresholds; confidence derives from agreement. Update `src/Njord.Tests/Enrichment/Features/AlertEnrichmentSpec.cs` and alert evaluator tests.
- [x] 3.2 Refactor `DerivedEnrichment` (`src/Njord/Enrichment/Features/DerivedEnrichment.cs`) and `DerivedResult` (`src/Njord/Domain/Analysis/DerivedResult.cs`) to compute from `ConsensusSnapshot`. Update `src/Njord.Tests/Enrichment/Features/DerivedEnrichmentSpec.cs` and derived result tests.
- [x] 3.3 Refactor `TrendEnrichment` (`src/Njord/Enrichment/Features/TrendEnrichment.cs`) and `TrendResult` (`src/Njord/Domain/Analysis/TrendResult.cs`) to compute from `ConsensusSnapshot` pairs. Update `src/Njord.Tests/Enrichment/Features/TrendEnrichmentSpec.cs` and trend result tests.
- [x] 3.4 Refactor `IndexEnrichment` (`src/Njord/Enrichment/Features/IndexEnrichment.cs`) and `IndexResult` (`src/Njord/Domain/Analysis/IndexResult.cs`) to compute from `ConsensusSnapshot`. Derive envelope from consensus spread/CI instead of per-model evaluation. Update `src/Njord.Tests/Enrichment/Features/IndexEnrichmentSpec.cs` and index result tests.
- [x] 3.5 Refactor `EnergyEnrichment` (`src/Njord/Enrichment/Features/EnergyEnrichment.cs`) and `EnergyResult` (`src/Njord/Domain/Analysis/EnergyResult.cs`) to compute from `ConsensusSnapshot`. Derive pessimistic envelope from consensus CI. Update `src/Njord.Tests/Enrichment/Features/EnergyEnrichmentSpec.cs` and energy result tests.

## 4. Pipeline Graph Restructure

- [x] 4.1 Remove `ConsensusEnrichment` registration from `src/Njord/Configuration/NjordServiceSetup.cs`. Delete `src/Njord/Enrichment/Features/ConsensusEnrichment.cs`.
- [x] 4.2 Restructure `EnrichmentActor` stream graph (`src/Njord/Enrichment/EnrichmentActor.cs`): broadcast `ModelSnapshot` to (1) History branch (raw), (2) Consensus `Select` stage → enrichment inline flow. Consensus `Select` calls `ConsensusSnapshot.Compute` per location. Inline flow receives `ConsensusSnapshot` instead of `ModelSnapshot`.
- [x] 4.3 Add `EgressEvent.ConsensusUpdate` variant (or reuse `EnrichmentUpdate` with type "consensus") originating from the consensus stage, fed to `EgressActor` separately from the enrichment inline flow.
- [x] 4.4 Update `src/Njord.Tests/Enrichment/EnrichmentActorSpec.cs` — test new graph topology: History gets raw snapshot, enrichments get `ConsensusSnapshot`, consensus egress events emitted.

## 5. Egress Adaptation

- [x] 5.1 Update `StatePayloadBuilder.FromConsensus` (`src/Njord/Mqtt/StatePayloadBuilder.cs`) to accept `ConsensusSnapshot` instead of `ConsensusResult`. Emit messages from `Hourly.Parameters` and `Daily.Parameters`.
- [x] 5.2 Update consensus discovery payload building — extract from deleted `ConsensusEnrichment.BuildDiscoveryPayload` into a standalone method (e.g., on `DiscoveryPayloadBuilder` or a new `ConsensusDiscovery` helper). Wire into `DiscoveryActor`.
- [x] 5.3 Update `MqttEgressActor` (`src/Njord/Mqtt/MqttEgressActor.cs`) to handle `EgressEvent.ConsensusUpdate` by calling `StatePayloadBuilder.FromConsensus` with `ConsensusSnapshot`.
- [x] 5.4 Update `EnrichmentProtoMapper` (`src/Njord/Grpc/EnrichmentProtoMapper.cs`) — `MapConsensus` accepts `ConsensusSnapshot`, maps `Hourly.Parameters` and `Daily.Parameters` to `ConsensusUpdate` proto. Deprecate `daily_summaries` field.
- [x] 5.5 Update `src/Njord.Tests/Mqtt/StatePayloadBuilderSpec.cs` and `src/Njord.Tests/Mqtt/DiscoveryPayloadBuilderSpec.cs` for new type.

## 6. Cleanup

- [x] 6.1 Remove old `ConsensusResult.Compute`, `ComputeHourly`, `ComputeDaily`, `ComputeHorizon`, `ComputeDailyCutoff` from `src/Njord/Domain/Analysis/ConsensusResult.cs` (keep the record definition if still needed as a wire type, otherwise fold `HorizonConsensus`/`ParameterConsensus` into their own file).
- [x] 6.2 Remove old `ConsensusEnrichmentSpec.cs` test files (`src/Njord.Tests/Enrichment/Features/ConsensusEnrichmentSpec.cs` and `src/Njord.Tests/Enrichment/ConsensusEnrichmentSpec.cs` if it exists).
- [x] 6.3 Remove `DailyConsensusSummary` references from gRPC proto mapper and proto definitions (`protos/njord/v2/common.proto` — mark `daily_summaries` as deprecated, keep field number reserved).

## Validation

```powershell
# Full test suite from src/
dotnet run --project Njord.Tests/Njord.Tests.csproj

# Build verification
dotnet build Njord.slnx

# Slopwatch from repo root
dotnet slopwatch
```
