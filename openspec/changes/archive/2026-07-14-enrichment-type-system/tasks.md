## 1. Interfaces and shared types

- [x] 1.1 Create `src/Njord/Enrichment/IEnrichmentFeature.cs`
- [x] 1.2 Create `src/Njord/Enrichment/IStatelessEnrichment.cs`
- [x] 1.3 Create `src/Njord/Enrichment/IStatefulEnrichment.cs`
- [x] 1.4 Create `src/Njord/Enrichment/IActorEnrichment.cs`
- [x] 1.5 Create `src/Njord/Mqtt/DiscoveryContext.cs`
- [x] 1.6 Write `src/Njord.Tests/Enrichment/EnrichmentFeatureContractSpec.cs`

## 2. EgressEvent and TopicScheme refactoring

- [x] 2.1 Modify `src/Njord/Egress/EgressEvent.cs` — replaced 7 records with `EnrichmentUpdate`
- [x] 2.2 Add parameterised TopicScheme methods
- [x] 2.3 Extract `BuildDeviceEnvelope` helper, made `BuildComponent` internal
- [x] 2.4 Update `src/Njord.Tests/Mqtt/TopicSchemeSpec.cs` — 3 new tests
- [x] 2.5 Update `src/Njord.Tests/Egress/EgressActorSpec.cs`

## 3. Feature class implementations

- [x] 3.1 ConsensusEnrichment
- [x] 3.2 AlertEnrichment
- [x] 3.3 DerivedEnrichment
- [x] 3.4 IndexEnrichment
- [x] 3.5 EnergyEnrichment
- [x] 3.6 TrendEnrichment
- [x] 3.7 HistoryEnrichment (with SelectAsync fix)
- [x] 3.8 ConsensusEnrichmentSpec
- [x] 3.9 AlertEnrichmentSpec
- [x] 3.10 TrendEnrichmentSpec
- [x] 3.11 HistoryEnrichmentSpec

## 4. Actor rewiring

- [x] 4.1 EnrichmentActor — feature registry loop
- [x] 4.2 DiscoveryActor — feature loop
- [x] 4.3 MqttEgressActor — dictionary dispatch
- [x] 4.4 ForecastHistoryActor — TimeProvider injection

## 5. DI registration and cleanup

- [x] 5.1 Register all 7 features in NjordServiceSetup
- [ ] 5.2 Remove old type-specific TopicScheme methods — deferred: still used by StatePayloadBuilder/DiscoveryPayloadBuilder delegates
- [ ] 5.3 Remove old Build* methods from DiscoveryPayloadBuilder — deferred: feature classes delegate to them
- [ ] 5.4 Remove old From* methods from StatePayloadBuilder — deferred: feature classes delegate to them
- [x] 5.5 Remove 7 enrichment records from EgressEvent

## 6. Test updates

- [x] 6.1 DiscoveryActorSpec — adapted constructor
- [x] 6.2 EnrichmentActorSpec — adapted constructor
- [x] 6.3 MqttEgressActorSpec — N/A (file does not exist)
- [x] 6.4 DiscoveryPayloadBuilderSpec — existing tests still pass (old methods remain as delegates)
- [x] 6.5 StatePayloadBuilderSpec — existing tests still pass (old methods remain as delegates)
- [x] 6.6 Verify snapshots — no changes needed (old code paths still active as delegates)
- [x] 6.7 ForecastHistoryActorSpec — TimeProvider added

## 7. Validation

- [x] 7.1 Unit tests: 392 pass (26 new)
- [x] 7.2 Integration tests: 7 pass
- [x] 7.3 Build: 0 errors, 0 warnings
- [x] 7.4 Slopwatch: 2 pre-existing warnings only
