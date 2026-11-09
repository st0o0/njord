## 1. Core domain extensions

- [x] 1.1 Add `OutputKind.Weather` and `OutputKind.Observation` to the `OutputKind` enum in `src/Njord.Core/Domain/OutputKind.cs`. Add `EntityPlatform` enum (`Weather`, `Sensor`, `BinarySensor`) in `src/Njord.Core/Domain/EntityPlatform.cs`. Add `EntityMetadata` record (`EntityPlatform Platform, string? DeviceClass, string? Icon, string? Unit`) in `src/Njord.Core/Domain/EntityMetadata.cs`. Extend `FeatureDescriptor` to include an `EntityMetadata` property. Tests: `src/Njord.Core.Tests/Domain/EntityMetadataTests.cs` (construction, equality), `src/Njord.Core.Tests/Domain/OutputKindTests.cs` (new members exist).

- [x] 1.2 Add `EntityMetadata` to all existing `FeatureDescriptor` instances across `src/Njord.Features/`. Each feature's descriptor gains the appropriate `EntityMetadata` based on its OutputKind and semantics (e.g. `Frost` -> `BinarySensor, "safety"`, `Commute` -> `Sensor, null, null, "%"`). No behavioral change; existing tests must still pass. Run all test projects to verify.

### Validation

```
dotnet build src/Njord.slnx
dotnet run --project src/Njord.Core.Tests/Njord.Core.Tests.csproj
dotnet run --project src/Njord.Features.Tests/Njord.Features.Tests.csproj
```

## 2. New result types

- [x] 2.1 Add `WeatherResult` in `src/Njord.Messages/Results/WeatherResult.cs`: a `FeatureResult` subclass carrying current conditions (temperature, humidity, wind speed, wind bearing, pressure, condition string, apparent temperature) and `IReadOnlyList<WeatherForecastPoint>` for hourly forecast. Add `WeatherForecastPoint` record with the same fields plus `DateTimeOffset Time`. Tests: `src/Njord.Messages.Tests/Results/WeatherResultTests.cs` (construction, property access).

- [x] 2.2 Add `ObservationResult` in `src/Njord.Messages/Results/ObservationResult.cs`: a `FeatureResult` subclass carrying `double Value`. Tests: `src/Njord.Messages.Tests/Results/ObservationResultTests.cs` (construction, property access).

### Validation

```
dotnet run --project src/Njord.Messages.Tests/Njord.Messages.Tests.csproj
```

## 3. Weather and Observation features

- [ ] 3.1 Add `WeatherRule` in `src/Njord.Features/Weather/WeatherRule.cs`: a `FeatureRule<ForecastInput>` with `OutputKind.Weather`, `FeatureCategory.Forecast`, `Name: "Weather"`, requirements for temperature/weather_code/humidity/wind_speed/wind_direction/pressure/apparent_temperature, `EntityMetadata(EntityPlatform.Weather)`. Always enabled (no config toggle or a config with `Enabled = true` and no override). `Evaluate` maps the current `ForecastPoint` to `WeatherResult` with current conditions and the full forecast array. Tests: `src/Njord.Features.Tests/Weather/WeatherRuleTests.cs` (evaluate with data, skip when no data, result structure).

- [ ] 3.2 Add sample Observation features to validate the pattern. At minimum: `Pm25ObservationRule` (AirQuality), `WaveHeightObservationRule` (Marine), `FloodDischargeObservationRule` (Flood), `UvIndexObservationRule` (Forecast). Each in `src/Njord.Features/Observations/<Name>/`. Each is a trivial `FeatureRule` that extracts one field and returns `ObservationResult`. Tests: `src/Njord.Features.Tests/Observations/<Name>Tests.cs` (evaluate with data, null when field missing).

- [ ] 3.3 Register `WeatherRule` and all Observation features in `src/Njord.Features/ServiceCollectionExtensions.cs` via `AddFeature<>()`. WeatherRule may need a dedicated registration path if it has no config class (always enabled). Run all feature tests.

### Validation

```
dotnet run --project src/Njord.Features.Tests/Njord.Features.Tests.csproj
dotnet run --project src/Njord.Architecture.Tests/Njord.Architecture.Tests.csproj
```

## 4. v3 Proto

- [ ] 4.1 Create `protos/njord/v3/njord.proto` with the v3 service definition: `NjordService` with `GetManifest`, `Subscribe`, `GetStatus`, `TriggerPoll`. Define all message types: `ManifestRequest`/`ManifestResponse`, `EntityDefinition` (entity_id, location, feature, platform, device_class, icon, unit, category, output_kind), `SubscribeRequest` (optional location filter), `EntityEvent` (oneof: snapshot, state_update, unavailable, available), `EntityStateUpdate` (entity_id, updated_at, value oneof with 7 types), `WeatherValue`, `AlertValue`, `ScoreValue`, `ConditionValue`, `TrendValue`, `ForecastValue`, `ObservationValue`, `StatusRequest`/`StatusResponse` (reuse v2 semantics), `TriggerPollRequest`/`TriggerPollResponse`. Use `google.protobuf.Timestamp` for times.

- [ ] 4.2 Wire the v3 proto into the build. Add it to `src/Njord.Grpc/Njord.Grpc.csproj` Protobuf items (Server). Verify `dotnet build` generates the C# stubs. Ensure v2 protos remain and still build.

### Validation

```
dotnet build src/Njord.slnx
```

## 5. ManifestBuilder

- [ ] 5.1 Create `ManifestBuilder` in `src/Njord.Grpc/ManifestBuilder.cs`. Takes `IEnumerable<IFeatureRule>`, `NjordOptions`, and `ActiveModels`. Builds `IReadOnlyList<EntityDefinition>` by iterating enabled rules x locations. Entity ID format: `{location_slug}_{feature_snake}` (primary weather: `{slug}_weather`, secondary model: `{slug}_{model_slug}_weather`). Register as singleton in DI. Tests: `src/Njord.Grpc.Tests/ManifestBuilderTests.cs` (multiple locations, disabled features excluded, secondary models, entity ID format, deterministic output).

### Validation

```
dotnet run --project src/Njord.Grpc.Tests/Njord.Grpc.Tests.csproj
```

## 6. Generic per-OutputKind mapping

- [ ] 6.1 Create v3 mapping extensions in `src/Njord.Grpc/Mapping/V3/`: `AlertValueMappingExtensions.cs`, `ScoreValueMappingExtensions.cs`, `ConditionValueMappingExtensions.cs`, `TrendValueMappingExtensions.cs`, `ForecastValueMappingExtensions.cs`, `ObservationValueMappingExtensions.cs`, `WeatherValueMappingExtensions.cs`. Each maps from the corresponding `FeatureResult` subclass to the v3 proto value type. One extension per OutputKind, no per-feature logic. Tests: `src/Njord.Grpc.Tests/Mapping/V3/<Kind>ValueMappingTests.cs` for each mapper.

- [ ] 6.2 Create `EntityStateUpdateMappingExtensions` in `src/Njord.Grpc/Mapping/V3/EntityStateUpdateMappingExtensions.cs`. Dispatches `FeatureResult` -> appropriate value mapper based on result base type (7-arm switch on OutputKind or result type). Returns `EntityStateUpdate` proto with entity_id and updated_at. Tests: `src/Njord.Grpc.Tests/Mapping/V3/EntityStateUpdateMappingTests.cs` (all 7 OutputKinds, unknown type throws).

### Validation

```
dotnet run --project src/Njord.Grpc.Tests/Njord.Grpc.Tests.csproj
```

## 7. GrpcConsumer v3 state model

- [ ] 7.1 Extend `GrpcConsumer` to support entity-keyed state alongside existing state. Add `ImmutableDictionary<string, EntityState>` keyed by entity_id. `EntityState` holds `FeatureResult` and `DateTimeOffset LastUpdated`. Build a startup lookup from registered `IFeatureRule[]`: `(Type resultType, string location) -> string entityId`. On each `PipelineEvent`, update both the existing v2 state and the new v3 entity state. Tests: `src/Njord.Grpc.Tests/GrpcConsumerEntityStateTests.cs` (feature result maps to entity_id, domain data maps to weather/observation entities, unknown result type is ignored).

- [ ] 7.2 Add `RequestEntityStream` message handling to `GrpcConsumer`. Returns `ISourceRef<EntityEvent>` using the same `Source.From(snapshot).Concat(hub)` pattern. Snapshot sends all current entity states as `EntityStateUpdate`. Live events flow from a new v3 BroadcastHub that receives `EntityEvent` mapped from `PipelineEvent`. Keep-alive 30s, buffer 256 DropHead, idle timeout 90s. Tests: `src/Njord.Grpc.Tests/GrpcConsumerStreamTests.cs` (snapshot on connect, live updates, keep-alive does not appear as event).

### Validation

```
dotnet run --project src/Njord.Grpc.Tests/Njord.Grpc.Tests.csproj
```

## 8. Availability tracking

- [ ] 8.1 Add availability tracking to `GrpcConsumer`. Track `DateTimeOffset` of last update per entity. Use `IWithTimers` to schedule a periodic check (every 60s). When an entity's last update exceeds `poll_interval * StaleFactor` (from `HealthOptions`, default 2.5), emit `EntityUnavailable` to the v3 hub. When fresh data arrives for a stale entity, emit `EntityAvailable`. The staleness threshold per entity comes from its required domain's poll interval. Tests: `src/Njord.Grpc.Tests/GrpcConsumerAvailabilityTests.cs` (entity becomes unavailable after threshold, becomes available on fresh data, non-stale entity stays available). Use `FakeTimeProvider` for time control.

### Validation

```
dotnet run --project src/Njord.Grpc.Tests/Njord.Grpc.Tests.csproj
```

## 9. NjordGrpcService (v3)

- [ ] 9.1 Create `NjordGrpcService` in `src/Njord.Grpc/NjordGrpcService.cs` implementing the v3 `NjordService.NjordServiceBase`. `GetManifest` returns the singleton manifest. `Subscribe` gets `ISourceRef<EntityEvent>` from GrpcConsumer via `RequestEntityStream`, applies KillSwitch + CancellationToken bridge, writes each `EntityEvent` to the gRPC response stream (same pattern as v2 `StreamDomainData`). Optional location filter in `SubscribeRequest`. `GetStatus` and `TriggerPoll` delegate to the same actors as v2 `OpsGrpcService`. Tests: `src/Njord.Grpc.Tests/NjordGrpcServiceTests.cs` (manifest returns correct count, subscribe streams events, status and trigger_poll work).

- [ ] 9.2 Register `NjordGrpcService` in the Host alongside existing v2 services. Both v2 and v3 services are mapped to Kestrel. Update `src/Njord/Configuration/GrpcSetup.cs` (or the relevant setup) to add `endpoints.MapGrpcService<NjordGrpcService>()`. Verify both v2 and v3 services respond.

### Validation

```
dotnet build src/Njord.slnx
dotnet run --project src/Njord.Grpc.Tests/Njord.Grpc.Tests.csproj
```

## 10. Integration verification

- [ ] 10.1 Run all test projects to verify no regressions. Existing v2 functionality must be unchanged.

```
dotnet build src/Njord.slnx
dotnet format src/Njord.slnx --verify-no-changes
```

Then run each test project:

```
dotnet run --project src/Njord.Core.Tests/Njord.Core.Tests.csproj
dotnet run --project src/Njord.Messages.Tests/Njord.Messages.Tests.csproj
dotnet run --project src/Njord.Features.Tests/Njord.Features.Tests.csproj
dotnet run --project src/Njord.Grpc.Tests/Njord.Grpc.Tests.csproj
dotnet run --project src/Njord.Pipeline.Tests/Njord.Pipeline.Tests.csproj
dotnet run --project src/Njord.Polling.Tests/Njord.Polling.Tests.csproj
dotnet run --project src/Njord.Budget.Tests/Njord.Budget.Tests.csproj
dotnet run --project src/Njord.OpenMeteo.Tests/Njord.OpenMeteo.Tests.csproj
dotnet run --project src/Njord.Persistence.Tests/Njord.Persistence.Tests.csproj
dotnet run --project src/Njord.Architecture.Tests/Njord.Architecture.Tests.csproj
dotnet run --project src/Njord.IntegrationTests/Njord.IntegrationTests.csproj
```

- [ ] 10.2 Update `AGENTS.md` and `CLAUDE.md` to document v3 proto, new OutputKinds, EntityMetadata, ManifestBuilder, and the unified Subscribe stream. Update the actor topology diagram to show v3 path.

- [ ] 10.3 Update `docs/data/features.json` with the new Weather and Observation features. Update the Config Builder if needed (`docs/.vitepress/theme/builder/`).
