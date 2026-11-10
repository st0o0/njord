## 1. EntityRegistry

- [x] 1.1 Create `src/Njord.Grpc/EntityRegistry.cs` with `EntityRegistration` record and `EntityRegistry` class. Constructor takes `IEnumerable<IFeatureRule>`, `IOptions<NjordOptions>`, `IOptions<ActiveModels>`. Builds `All` list and two lookup dictionaries: `(Type, string) -> entityId` for non-observations, `(string FeatureName, string Location) -> entityId` for observations. Reuse `Slugify` and `ToSnakeCase` as internal static methods on EntityRegistry.
- [x] 1.2 Create `src/Njord.Grpc.Tests/EntityRegistryTests.cs`. Tests: registry built from features and locations, Find for alert/score/condition/observation results, Find returns null for disabled features, observation results resolve by FeatureName, weather entities include model variants. Port relevant tests from `EntityIdLookupTests.cs` and `ManifestBuilderTests.cs`.
- [x] 1.3 Create `src/Njord.Grpc/Mapping/ManifestMappingExtensions.cs` with `ToEntityDefinition()` extension on `EntityRegistration`. Move platform/output-kind mapping from ManifestBuilder.
- [x] 1.4 Delete `src/Njord.Grpc/EntityIdLookup.cs` and `src/Njord.Grpc/ManifestBuilder.cs`. Delete `src/Njord.Grpc.Tests/EntityIdLookupTests.cs` and `src/Njord.Grpc.Tests/ManifestBuilderTests.cs`.
- [x] 1.5 Update `src/Njord.Grpc/Configuration/GrpcSetup.cs`: register `EntityRegistry` as singleton, remove `ManifestBuilder` and `EntityIdLookup` registrations.
- [x] 1.6 Update `src/Njord.Grpc/NjordGrpcService.cs`: inject `EntityRegistry` instead of `ManifestBuilder`. `GetManifest` maps `registry.All` via `ToEntityDefinition()`.

## 2. ConsumerEvent domain types

- [x] 2.1 Create `src/Njord.Grpc/ConsumerEvent.cs` with abstract record `ConsumerEvent` and sealed records: `ConsumerSnapshot(IReadOnlyList<EntityState> States)`, `EntityStateChanged(string EntityId, FeatureResult Result, DateTimeOffset UpdatedAt)`, `EntityBecameUnavailable(string EntityId, DateTimeOffset Since)`, `EntityBecameAvailable(string EntityId, DateTimeOffset RecoveredAt)`.
- [x] 2.2 Update `src/Njord.Grpc/EntityStreamMessages.cs`: change `RequestEntityStreamCompleted.SourceRef` from `ISourceRef<Proto.EntityEvent>` to `ISourceRef<ConsumerEvent>`.

## 3. GrpcConsumer refactor

- [x] 3.1 Update `src/Njord.Grpc/GrpcConsumer.cs`: inject `EntityRegistry` instead of `EntityIdLookup`. Change hub type from `Proto.EntityEvent` to `ConsumerEvent`. Remove all `using` for Proto namespace.
- [x] 3.2 Update `OnPipelineEvent`: use `_registry.Find(result)` and publish `EntityStateChanged(entityId, result, now)` to the hub instead of `Proto.EntityEvent { StateUpdate = ... }`.
- [x] 3.3 Update `OnCheckAvailability`: publish `EntityBecameUnavailable` and `EntityBecameAvailable` domain events instead of Proto types.
- [x] 3.4 Update `HandleEntityStreamRequest`: build `ConsumerSnapshot` from `_state.EntityStates`, build stale events as `EntityBecameUnavailable`, concat with hub source. All state access in the Receive handler, stream stages capture only the immutable list.
- [x] 3.5 Add default state seeding in `PreStart`: after materializing the hub, iterate `_registry.All` and seed `_state` with default `FeatureResult` per OutputKind (Alert: Severity.None, Condition: Active=false, Score: 0, Observation: 0; skip Weather/Trend/Forecast). Add a helper `DefaultResultFactory` or switch in `GrpcConsumerStateExtensions`.

## 4. Proto mapping at the boundary

- [x] 4.1 Create `src/Njord.Grpc/Mapping/ConsumerEventMappingExtensions.cs` with `ToProto()` extension on `ConsumerEvent`. Dispatches to existing `ToEntityStateUpdate()`, builds `Proto.EntityEvent` variants for snapshot/state-change/unavailable/available.
- [x] 4.2 Update `src/Njord.Grpc/NjordGrpcService.cs` `Subscribe()`: receive `ISourceRef<ConsumerEvent>`, add `.Select(e => e.ToProto())` before the filter stage. Adjust `Filter` to work on `Proto.EntityEvent` (unchanged).
- [x] 4.3 Create `src/Njord.Grpc.Tests/Mapping/ConsumerEventMappingTests.cs`: test each ConsumerEvent variant maps to the correct Proto.EntityEvent oneof case.

## 5. Test updates

- [x] 5.1 Update `src/Njord.Grpc.Tests/GrpcConsumerFakePipeline.cs`: no changes needed (it already works with `PipelineEvent`).
- [x] 5.2 Update `src/Njord.Grpc.Tests/GrpcConsumerStreamTests.cs`: adapt to ConsumerEvent hub type. Verify snapshot includes default-state entities. Verify non-triggered alerts appear with Severity.None.
- [x] 5.3 Update `src/Njord.Grpc.Tests/GrpcConsumerEntityStateTests.cs`: adapt to use EntityRegistry instead of EntityIdLookup.
- [x] 5.4 Update `src/Njord.Grpc.Tests/GrpcConsumerAvailabilityTests.cs`: adapt availability events to domain types.
- [x] 5.5 Update `src/Njord.Grpc.Tests/NjordGrpcServiceTests.cs`: GetManifest uses EntityRegistry.

## 6. Validation

- [x] 6.1 Build: `cd src && dotnet build Njord.slnx`
- [x] 6.2 Format: `cd src && dotnet format Njord.slnx --verify-no-changes`
- [x] 6.3 Run all test projects sequentially:
  ```bash
  cd src
  for p in Njord.*Tests; do
    [ "$p" = Njord.Tests.Shared ] && continue
    dotnet run --project "$p/$p.csproj" --no-build
  done
  ```
- [x] 6.4 Verify architecture tests pass (zone rules: no Proto in consumer internals)
