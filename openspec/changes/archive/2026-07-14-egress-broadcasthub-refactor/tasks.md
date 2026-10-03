## 1. EgressEvent type and EgressActor messages

- [x] 1.1 Define `EgressEvent` abstract record with 8 sealed variants (PerModelUpdate, ConsensusUpdate, AlertUpdate, DerivedUpdate, TrendUpdate, IndexUpdate, EnergyUpdate, HistoryUpdate) in `Njord.Egress`
- [x] 1.2 Define `RequestEgressSink` / `EgressSinkResponse` and `RequestEgressSource` / `EgressSourceResponse` message types in `Njord.Egress`
- [x] 1.3 Write tests: EgressEvent variants carry correct domain data, pattern matching is exhaustive

## 2. Rebuild EgressActor with MergeHub + BroadcastHub

- [x] 2.1 Rewrite `EgressActor` to materialize `MergeHub.Source<EgressEvent>` → `BroadcastHub.Sink<EgressEvent>` in PreStart, pre-materialized (buffer sizes: MergeHub per-producer 8, BroadcastHub 64)
- [x] 2.2 Handle `RequestEgressSink` — vend `ISinkRef<EgressEvent>` connected to MergeHub
- [x] 2.3 Handle `RequestEgressSource` — vend `ISourceRef<EgressEvent>` connected to BroadcastHub
- [x] 2.4 Remove `RegisterPublisher`, `UnregisterPublisher`, `PublishStateResult` message types and all Tell-forwarding logic
- [x] 2.5 Write tests: SinkRef and SourceRef vending, end-to-end event flow through hub

## 3. ModelStateActor (rename from MqttPublisherActor)

- [x] 3.1 Create `ModelStateActor` in `Njord.Egress` — requests `ISourceRef<FetchOutcome>` from PipelineActor and `ISinkRef<EgressEvent>` from EgressActor
- [x] 3.2 Materialize stream graph: `sourceRef.Source → Collect(Success) → SelectMany(BuildPerHorizon → PerModelUpdate) → egressSinkRef.Sink` with dedup on (location, model, horizon)
- [x] 3.3 Write tests: FetchOutcome.Success → PerModelUpdate emission, dedup skips unchanged, Failure dropped
- [x] 3.4 Delete `MqttPublisherActor.cs` from `Njord.Mqtt`

## 4. Decouple EnrichmentActor from Njord.Mqtt

- [x] 4.1 Change `EnrichmentActor` to request `ISinkRef<EgressEvent>` from `EgressActor` instead of `ISinkRef<MqttMessage>` from `MqttConnectionActor`
- [x] 4.2 Rewrite 7 sub-graph `MaterializeXxxConsumer` methods: produce `EgressEvent.XxxUpdate` instead of `MqttMessage`, remove `StatePayloadBuilder.From*` calls, remove `lastPublished` dedup dictionaries
- [x] 4.3 Remove `using Njord.Mqtt` and all references to `MqttMessage`, `MqttSinkResponse`, `RequestMqttSink` from `EnrichmentActor.cs`
- [x] 4.4 Update enrichment tests to verify `EgressEvent` output instead of `MqttMessage`

## 5. MqttEgressActor

- [x] 5.1 Create `MqttEgressActor` in `Njord.Mqtt` — requests `ISourceRef<EgressEvent>` from EgressActor and `ISinkRef<MqttMessage>` from MqttConnectionActor
- [x] 5.2 Materialize stream graph: `egressSourceRef.Source → SelectMany(MapToMqttMessages) → mqttSinkRef.Sink` with per-topic dedup
- [x] 5.3 Implement `MapToMqttMessages` switch over all 8 EgressEvent variants using `StatePayloadBuilder.From*` and `TopicScheme`
- [x] 5.4 Write tests: each EgressEvent variant maps to correct MqttMessage(s), dedup skips unchanged, wire format matches previous output

## 6. Actor registration and wiring

- [x] 6.1 Update `NjordActorSystemSetup` — remove `MqttPublisherActor` registration, add `ModelStateActor` and `MqttEgressActor` registrations
- [x] 6.2 Verify `MqttConnectionActor` no longer needs to serve SinkRefs to `EnrichmentActor` — only `MqttEgressActor` and `DiscoveryActor` request them
- [x] 6.3 Run full test suite, fix any broken references to removed types (`RegisterPublisher`, `PublishStateResult`, `MqttPublisherActor`)

## 7. Cleanup and verification

- [x] 7.1 Verify `Njord.Enrichment` has no compile-time dependency on `Njord.Mqtt` (no using directives, no type references)
- [x] 7.2 Verify `Njord.Egress` has no compile-time dependency on `Njord.Mqtt`
- [x] 7.3 Run `dotnet slopwatch` from repo root and verify no regressions
- [x] 7.4 Run full test suite — all green
