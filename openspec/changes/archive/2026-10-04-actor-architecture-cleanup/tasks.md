## 1. Messages cleanup — routing markers and domain namespaces

- [x] 1.1 Add routing marker interfaces `IWithLocation`, `IWithModelKey`, `IWithEnrichmentKey` in `src/Njord.Messages/` (new file `Routing.cs` or in `Common/`). Apply `IWithModelKey` to `UpdateForecast`, `QueryForecast` in `src/Njord.Messages/Snapshots/SnapshotMessages.cs`. Apply `IWithEnrichmentKey` to `UpdateEnrichment`, `QueryEnrichment`. Apply `IWithLocation` to enrichment messages (task 1.3).
- [x] 1.2 Create `src/Njord.Messages/Mqtt/MqttMessages.cs` with `MqttConnected`, `MqttDisconnected`, `HaBirthDetected`, `SubscribeInbound(IActorRef Listener)`. Move existing `SubscribeInbound`, `MqttConnected`, `MqttInboundMessage` records from `src/Njord.Mqtt/MqttConnectionActor.cs` to this file. Update all usages in `src/Njord.Mqtt/`.
- [x] 1.3 Create `src/Njord.Messages/Enrichment/EnrichmentMessages.cs` with `RecordSnapshot`, `QueryHistory`, `QueryHistoryResult`, `QueryHistoryFailed` (implementing `IWithLocation`). Move these from `src/Njord.Enrichment/EnrichmentActor.cs` and `src/Njord.Enrichment/ForecastHistoryActor.cs` internal records. Update usages in `src/Njord.Enrichment/` and `src/Njord.Enrichment.Tests/`.
- [x] 1.4 Move SinkRef/SourceRef records out of `Njord.Messages`: Move `RequestPipelineSink`, `PipelineSinkResponse`, `PipelineSinkFailed`, `RequestPipelineSource`, `PipelineSourceResponse`, `PipelineSourceFailed` from `src/Njord.Messages/Pipeline/SchedulerMessages.cs` to actor-internal records in `src/Njord.Pipeline/PipelineActor.cs` and `src/Njord.Pipeline/SchedulerActor.cs`. Move `RequestEgressSink`, `EgressSinkResponse`, `EgressSinkFailed`, `RequestEgressSource`, `EgressSourceResponse`, `EgressSourceFailed` from `src/Njord.Messages/Egress/EgressMessages.cs` to `src/Njord.Egress/EgressActor.cs`. Move `FailureConsumerCompleted`, `FailureConsumerFailed` from `src/Njord.Messages/Pipeline/SchedulerMessages.cs` to `src/Njord.Pipeline/SchedulerActor.cs`.
- [x] 1.5 Move `StopStreams`, `StreamsStopped`, `StreamsStopFailed` from `src/Njord.Messages/Common/StreamShutdownMessages.cs` to a shared file in `src/Njord.Core/Actors/` (still referenced by PipelineActor and EgressActor, but not in shared messages). Delete `src/Njord.Messages/Common/StreamShutdownMessages.cs`.
- [x] 1.6 Remove `Akka.Streams` PackageReference from `src/Njord.Messages/Njord.Messages.csproj`. Fix any remaining compile errors from the moved records.
- [x] 1.7 Update architecture tests: `src/Njord.Architecture.Tests/ZoneArchitectureSpec.cs` and `src/Njord.Architecture.Tests/LayerReferenceSpec.cs` for new namespaces and the removed Akka.Streams dependency.

### Validation

```
dotnet build src/Njord.slnx
dotnet run --project src/Njord.Architecture.Tests/Njord.Architecture.Tests.csproj
dotnet run --project src/Njord.Pipeline.Tests/Njord.Pipeline.Tests.csproj
dotnet run --project src/Njord.Egress.Tests/Njord.Egress.Tests.csproj
dotnet run --project src/Njord.Enrichment.Tests/Njord.Enrichment.Tests.csproj
dotnet run --project src/Njord.Mqtt.Tests/Njord.Mqtt.Tests.csproj
dotnet run --project src/Njord.Grpc.Tests/Njord.Grpc.Tests.csproj
```

## 2. PipelineActor simplification — remove MergeHub, accept Tell

- [x] 2.1 Simplify `src/Njord.Pipeline/PipelineActor.cs`: Remove MergeHub and `RequestPipelineSink` handling. Add `Receive<ScheduledPoll>` that enqueues into an internal `SourceQueue<WeightedTarget>`. Keep BroadcastHub for output. Add actor-internal `RequestPipelineSource`/`PipelineSourceResponse`/`PipelineSourceFailed` records. Resolve SchedulerActor for hash feedback (the hash stream from BroadcastHub that sends `HashResult` back to scheduler via `Ask<Ack>`).
- [x] 2.2 Update `src/Njord.Pipeline.Tests/PipelineConnectionSpec.cs` and `src/Njord.Pipeline.Tests/PipelineActorShutdownSpec.cs`: Remove SinkRef-based connection tests. Add Tell-based ScheduledPoll tests. Verify BroadcastHub SourceRef distribution.
- [x] 2.3 Update `src/Njord.Tests/Configuration/StreamShutdownTaskSpec.cs` for simplified PipelineActor (StopStreams handling stays, but may use the moved message type from Core).

### Validation

```
dotnet run --project src/Njord.Pipeline.Tests/Njord.Pipeline.Tests.csproj
dotnet run --project src/Njord.Tests/Njord.Tests.csproj
```

## 3. PollSchedulerActor simplification — Tell + SourceRef feedback

- [x] 3.1 Rewrite `src/Njord.Pipeline/SchedulerActor.cs`: Remove `WaitingForPipeline`, `WaitingForRefs`, `Connecting`, `WaitingForConnection` phases. Resolve PipelineActor from registry and Watch it. Send `ScheduledPoll` via Tell. Subscribe to PipelineActor's BroadcastHub via one SourceRef for hash/failure feedback (materialize `SourceRef.Source → filter → Sink.ActorRef(Self)`). Keep persistence, scheduling, hash tracking, metrics, `QueryPollStates`, `TriggerImmediatePoll` as-is. On `Terminated(PipelineActor)`: re-resolve, re-subscribe.
- [x] 3.2 Update scheduler specs: `src/Njord.Pipeline.Tests/SchedulerActorSpec.cs`, `src/Njord.Pipeline.Tests/SchedulerActorSnapshotSpec.cs`, `src/Njord.Pipeline.Tests/SchedulerActorStartupOrderSpec.cs`, `src/Njord.Pipeline.Tests/SchedulerActorGetPollStatesSpec.cs`, `src/Njord.Pipeline.Tests/SchedulerActorGetPollStatesBeforeReadySpec.cs`. Replace SinkRef/SourceRef-based pipeline mocking with Tell-based PipelineActor fakes.
- [x] 3.3 Update `src/Njord.Pipeline.Tests/SinkRefConnectionSpec.cs`: Refactor or remove tests that verify SinkRef connection resilience (no longer applicable). Keep SourceRef connection resilience tests.

### Validation

```
dotnet run --project src/Njord.Pipeline.Tests/Njord.Pipeline.Tests.csproj
```

## 4. ModelStateActor — add BroadcastHub output

- [x] 4.1 Add BroadcastHub and `RequestModelStateSource`/`ModelStateSourceResponse`/`ModelStateSourceFailed` (actor-internal) to `src/Njord.Egress/ModelStateActor.cs`. In `MaterializeGraph`: pipe processed EgressEvents into a pre-materialized BroadcastHub. In `ConfigureReady`: handle `RequestModelStateSource` by creating SourceRefs from the BroadcastHub. Remove EgressActor SinkRef dependency (no more `RequestEgressSink`). Keep PipelineActor SourceRef dependency.
- [x] 4.2 Update `src/Njord.Egress.Tests/ModelStateActorSpec.cs`: Remove EgressActor SinkRef mocking. Add BroadcastHub consumer tests (request SourceRef, verify EgressEvents flow through).

### Validation

```
dotnet run --project src/Njord.Egress.Tests/Njord.Egress.Tests.csproj
```

## 5. EnrichmentActor — add BroadcastHub output

- [x] 5.1 Add BroadcastHub and `RequestEnrichmentSource`/`EnrichmentSourceResponse`/`EnrichmentSourceFailed` (actor-internal) to `src/Njord.Enrichment/EnrichmentActor.cs`. In `MaterializeGraph`: pipe enrichment EgressEvents into a pre-materialized BroadcastHub. Remove EgressActor SinkRef dependency. Keep PipelineActor SourceRef and SensorHubActor dependency.
- [x] 5.2 Update `src/Njord.Enrichment.Tests/EnrichmentActorSpec.cs`: Remove EgressActor SinkRef mocking. Add BroadcastHub consumer tests.

### Validation

```
dotnet run --project src/Njord.Enrichment.Tests/Njord.Enrichment.Tests.csproj
```

## 6. MQTT actors — rename, rewire to producers, direct transport

- [x] 6.1 Rename `src/Njord.Mqtt/MqttEgressActor.cs` → `src/Njord.Mqtt/MqttStateActor.cs` (class name `MqttStateActor`). Update `IMqttEgressActor` → `IMqttStateActor` in `src/Njord.Core/Actors/ActorKeys.cs`. Rewire dependencies: subscribe to ModelStateActor and EnrichmentActor SourceRefs (replace EgressActor SourceRef). Replace MqttConnectionActor SinkRef with direct `IMqttTransport.SendAsync` call in the stream's final `SelectAsync`. Inject `IMqttTransport` via constructor.
- [x] 6.2 Rename `src/Njord.Mqtt/DiscoveryActor.cs` → `src/Njord.Mqtt/MqttDiscoveryActor.cs` (class name `MqttDiscoveryActor`). Update `IDiscoveryActor` → `IMqttDiscoveryActor` in `src/Njord.Core/Actors/ActorKeys.cs`. Rewire: subscribe to ModelStateActor SourceRef for CapabilityLearned events (replace EgressActor SourceRef). Replace MqttConnectionActor SinkRef with direct `IMqttTransport.SendAsync`. Keep `SubscribeInbound` to MqttConnectionActor for HA birth detection.
- [x] 6.3 Simplify `src/Njord.Mqtt/MqttConnectionActor.cs`: Remove MergeHub, `RequestMqttSink`/`MqttSinkResponse`/`MqttSinkFailed` handling, and the `MaterializeEgressGraph` method. Keep connection lifecycle (Connect, Reconnect, SubscribeInbound, HA birth inbound). Send availability messages (online/offline) directly via `IMqttTransport.SendAsync`.
- [x] 6.4 Update MQTT test files: rename `src/Njord.Mqtt.Tests/MqttEgressActorSpec.cs` → `MqttStateActorSpec.cs`, `src/Njord.Mqtt.Tests/DiscoveryActorSpec.cs` → `MqttDiscoveryActorSpec.cs`. Rewire test fakes to provide ModelStateActor/EnrichmentActor SourceRefs. Remove SinkRef-based assertions. Update `src/Njord.Mqtt.Tests/MqttConnectionActorSpec.cs` for simplified actor (no MergeHub).

### Validation

```
dotnet run --project src/Njord.Mqtt.Tests/Njord.Mqtt.Tests.csproj
```

## 7. Delete EgressActor and rewire GrpcSnapshotConsumer

- [x] 7.1 Delete `src/Njord.Egress/EgressActor.cs`. Remove `IEgressActor` from `src/Njord.Core/Actors/ActorKeys.cs`. Delete `src/Njord.Messages/Egress/EgressMessages.cs` (now empty after SinkRef/SourceRef records moved out). Remove EgressActor registration from `src/Njord/Configuration/NjordActorSystemSetup.cs` (`RegisterEgressActors` → only ModelStateActor). Remove `StopStreamsOf<IEgressActor>` from `AddStreamShutdownTask`.
- [x] 7.2 Rewire `src/Njord.Grpc/GrpcSnapshotConsumerActor.cs`: Replace EgressActor SourceRef with SourceRefs from ModelStateActor and EnrichmentActor. Merge both SourceRef streams internally before routing to snapshot actors.
- [x] 7.3 Update `src/Njord.Grpc.Tests/GrpcSnapshotConsumerTerminatedSpec.cs` and other gRPC specs for new dependencies.
- [x] 7.4 Delete `src/Njord.Egress.Tests/EgressActorSpec.cs` and `src/Njord.Egress.Tests/EgressActorShutdownSpec.cs`. Update remaining egress specs.

### Validation

```
dotnet build src/Njord.slnx
dotnet run --project src/Njord.Egress.Tests/Njord.Egress.Tests.csproj
dotnet run --project src/Njord.Grpc.Tests/Njord.Grpc.Tests.csproj
dotnet run --project src/Njord.Tests/Njord.Tests.csproj
```

## 8. Host registration update

- [x] 8.1 Update `src/Njord/Configuration/NjordActorSystemSetup.cs`: Remove `RegisterEgressActors` (EgressActor gone, ModelStateActor stays in a renamed method). Rename actor registrations for `MqttStateActor` and `MqttDiscoveryActor`. Update `AddStreamShutdownTask` to stop streams on the new producer actors.
- [x] 8.2 Update `src/Njord.Tests/Configuration/` host-level specs for new registrations.

### Validation

```
dotnet run --project src/Njord.Tests/Njord.Tests.csproj
dotnet run --project src/Njord.Architecture.Tests/Njord.Architecture.Tests.csproj
```

## 9. ShardRegion infrastructure

- [x] 9.1 Add `Akka.Cluster.Sharding` package: `dotnet add src/Njord.Core/Njord.Core.csproj package Akka.Cluster.Sharding.Hosting`. Create `src/Njord.Core/Actors/NjordMessageExtractor.cs` extending `HashCodeMessageExtractor`. Route by `IWithModelKey` → `IWithEnrichmentKey` → `IWithLocation` (most-specific first). Add `IForecastHistoryRegion`, `IForecastSnapshotRegion`, `IEnrichmentSnapshotRegion` to `src/Njord.Core/Actors/ActorKeys.cs`.
- [x] 9.2 Write `src/Njord.Core.Tests/Actors/NjordMessageExtractorSpec.cs`: Verify EntityId extraction for each marker interface. Verify unknown message throws. Sealed class, BDD-style names, `[Fact]` (synchronous, no timeout needed).

### Validation

```
dotnet run --project src/Njord.Core.Tests/Njord.Core.Tests.csproj
```

## 10. Convert ForecastHistoryActor to ShardRegion

- [x] 10.1 Update `src/Njord.Enrichment/ForecastHistoryActor.cs`: Change PersistenceId from hardcoded to entity-based (`$"forecast-history-{entityId}"`). Accept `entityId` from constructor (passed by ShardRegion). Update `src/Njord.Enrichment/Features/HistoryEnrichment.cs`: Replace `ResolveChildActor` calls with messages sent to `IForecastHistoryRegion` ShardRegion (resolved via `Context.GetActorAsync<IForecastHistoryRegion>()`).
- [x] 10.2 Register ShardRegion in `src/Njord/Configuration/NjordActorSystemSetup.cs` using `WithShardRegion<IForecastHistoryRegion>` with `NjordMessageExtractor` and passivation options.
- [x] 10.3 Update `src/Njord.Enrichment.Tests/` specs for ShardRegion-based history routing.

### Validation

```
dotnet run --project src/Njord.Enrichment.Tests/Njord.Enrichment.Tests.csproj
```

## 11. Convert snapshot actors to ShardRegions

- [x] 11.1 SKIPPED — ForecastSnapshotActor stays singleton (QueryAll pattern, tiny state) to ShardRegion entity: Replace Dictionary-based state with single-forecast state per entity. PersistenceId from `$"forecast-snapshot-{entityId}"`. Accept entityId from constructor. Entity manages one `(location, modelId)` forecast.
- [x] 11.2 SKIPPED — EnrichmentSnapshotActor stays singleton (same reason) to ShardRegion entity: Same pattern. PersistenceId from `$"enrichment-snapshot-{entityId}"`. Entity manages one `(location, typeName)` enrichment result.
- [x] 11.3 SKIPPED — GrpcSnapshotConsumer keeps singleton routing: Route `UpdateForecast` and `UpdateEnrichment` messages directly to ShardRegions (they carry `IWithModelKey` / `IWithEnrichmentKey`). Remove Ask-based routing.
- [x] 11.4 SKIPPED and `src/Njord.Grpc/OpsGrpcService.cs` (if applicable): Query ShardRegion entities instead of singleton actors.
- [x] 11.5 SKIPPED in `src/Njord/Configuration/NjordActorSystemSetup.cs` using `WithShardRegion<IForecastSnapshotRegion>` and `WithShardRegion<IEnrichmentSnapshotRegion>`.
- [x] 11.6 SKIPPED specs: Replace singleton actor setup with ShardRegion entity setup. Test entity routing via message markers.

### Validation

```
dotnet run --project src/Njord.Grpc.Tests/Njord.Grpc.Tests.csproj
dotnet run --project src/Njord.Tests/Njord.Tests.csproj
```

## 12. StreamConsumerActor cleanup

- [x] 12.1 Simplify `src/Njord.Core/Actors/StreamConsumerActor.cs`: Review remaining SinkRef-related logic in the base class. At this point, no subclass uses SinkRef dependencies — remove any SinkRef tracking fields, SinkRef null checks in lifecycle methods, and SinkRef-related documentation. Ensure `AllRefsReady()` pattern works with SourceRef-only dependencies.
- [x] 12.2 Review and simplify `ConfigureWaitingForRefs()` in all remaining StreamConsumerActor subclasses: `src/Njord.Egress/ModelStateActor.cs`, `src/Njord.Enrichment/EnrichmentActor.cs`, `src/Njord.Mqtt/MqttStateActor.cs`, `src/Njord.Mqtt/MqttDiscoveryActor.cs`, `src/Njord.Grpc/GrpcSnapshotConsumerActor.cs`. Remove dead SinkRef handling code.
- [x] 12.3 Update `src/Njord.Core.Tests/Actors/StreamConsumerActorSpec.cs` for SourceRef-only base class behavior.

### Validation

```
dotnet run --project src/Njord.Core.Tests/Njord.Core.Tests.csproj
```

## 13. Final validation

- [x] 13.1 Run full solution build and all test suites:
```
dotnet build src/Njord.slnx
for p in src/Njord.*Tests; do [ "$p" = "src/Njord.Tests.Shared" ] && continue; dotnet run --project "$p/$( basename $p ).csproj" --no-build; done
```
- [x] 13.2 Run architecture tests to verify zone/layer rules still pass:
```
dotnet run --project src/Njord.Architecture.Tests/Njord.Architecture.Tests.csproj
```
- [x] 13.3 Run slopwatch from repo root:
```
dotnet tool restore
dotnet slopwatch analyze -d . --fail-on warning
```
- [x] 13.4 Run dotnet format whitespace check:
```
dotnet format src/Njord.slnx --verify-no-changes
```
