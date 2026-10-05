## 1. Shared Test Infrastructure

- [x] 1.1 Create `src/Njord.Tests.Shared/TestOptionsMonitor.cs` — generic `TestOptionsMonitor<T> : IOptionsMonitor<T>` with `Update(T)` method that fires `OnChange` listeners
- [x] 1.2 Replace `MutableOptionsMonitor` in `src/Njord.Grpc.Tests/OpsGrpcServiceSpec.cs` with `TestOptionsMonitor<NjordOptions>`
- [x] 1.3 Replace `MutableOptionsMonitor`/`FakeOptionsMonitor` in `src/Njord.Grpc.Tests/AdminGrpcServiceSpec.cs` with `TestOptionsMonitor<NjordOptions>`
- [x] 1.4 Replace duplicated options monitor fake in `src/Njord.Pipeline.Tests/PipelineConnectionSpec.cs` (find the file) with `TestOptionsMonitor<T>`

## 2. Extract MqttOptions

- [x] 2.1 Add `public const string SectionName = "Njord:Mqtt"` to `src/Njord.Core/Configuration/MqttOptions.cs`; remove the `Mqtt` property from `NjordOptions`
- [x] 2.2 Update `src/Njord.Mqtt/MqttServiceCollectionExtensions.cs` `AddNjordMqtt()` to accept `IConfiguration`, call `AddOptions<MqttOptions>().Bind(config.GetSection(MqttOptions.SectionName)).ValidateOnStart()`
- [x] 2.3 Update `MqttConnectionActor` to inject `IOptions<MqttOptions>` instead of extracting from `IOptions<NjordOptions>`
- [x] 2.4 Update `MqttDiscoveryActor`, `MqttStateActor` to inject `IOptions<MqttOptions>` where they use MQTT config
- [x] 2.5 Update all MQTT test specs to construct with the new options type

## 3. Extract GrpcOptions

- [x] 3.1 Add `public const string SectionName = "Njord:Grpc"` to `src/Njord.Core/Configuration/GrpcOptions.cs`; remove the `Grpc` property from `NjordOptions`
- [x] 3.2 Update `src/Njord.Grpc/GrpcServiceCollectionExtensions.cs` (GrpcOptions.Port is unused — no registration needed) `AddNjordGrpc()` to accept `IConfiguration` and register `GrpcOptions`
- [x] 3.3 No gRPC services read `GrpcOptions` — no changes needed to inject `IOptions<GrpcOptions>`
- [x] 3.4 No gRPC tests reference GrpcOptions — no changes needed

## 4. Extract SensorOptions

- [x] 4.1 Add `public const string SectionName = "Njord:Sensors"` to `src/Njord.Core/Configuration/SensorOptions.cs`; remove from `NjordOptions`
- [x] 4.2 Move `SensorOptionsValidator` to validate `IValidateOptions<SensorOptions>` instead of `IValidateOptions<NjordOptions>`
- [x] 4.3 Register `SensorOptions` in `AddNjordPipeline()` or a new `AddNjordSensors()` extension
- [x] 4.4 Update `SensorHubActor` and `EnrichmentActor` to inject `IOptions<SensorOptions>`
- [x] 4.5 Update sensor test specs

## 5. Extract EnrichmentOptions

- [x] 5.1 Add `public const string SectionName = "Njord:Enrichment"` to `EnrichmentOptions`; remove from `NjordOptions`
- [x] 5.2 Enrichment actors now inject IOptions&lt;EnrichmentOptions&gt; directly (`ConsensusOptionsValidator`, `HistoryOptionsValidator`, `IndexOptionsValidator`) to validate their respective sub-options under `EnrichmentOptions`
- [x] 5.3 Register `EnrichmentOptions` in `AddNjordEnrichment()`
- [x] 5.4 Update enrichment actors/features to inject `IOptions<EnrichmentOptions>`
- [x] 5.5 Update enrichment test specs

## 6. Extract PersistenceOptions

- [x] 6.1 Add `public const string SectionName = "Njord:Persistence"` to `PersistenceOptions`; keep `PersistencePath` on `NjordOptions` for the DB file path
- [x] 6.2 PersistenceOptions consumed only by host's `NjordActorSystemSetup.ConfigureSystem()` — registration stays in host, no feature library owns it
- [x] 6.3 `ConfigureSystem()` already receives `NjordOptions` directly (not via DI) — accesses `.Persistence` inline, no injection change needed

## 7. Slim NjordServiceSetup and NjordOptionsValidator

- [x] 7.1 Remove sub-option registration from `NjordServiceSetup.SetupServices()` — feature libs now handle their own
- [x] 7.2 Remove sub-option validators from `NjordServiceSetup` registration — feature libs register their own
- [x] 7.3 `NjordOptionsValidator` already only validates cross-cutting concerns to only validate cross-cutting concerns (locations, models, persistence path)
- [x] 7.4 All host tests pass with new structure and other host tests for the new structure

## Validation

```bash
# From src/
dotnet build Njord.slnx
for p in Njord.*Tests; do
  [ "$p" = Njord.Tests.Shared ] && continue
  dotnet run --project "$p/$p.csproj" --no-build
done
# Slopwatch (from repo root)
dotnet slopwatch analyze -d . --fail-on warning
# Format
dotnet format whitespace --verify-no-changes Njord.slnx
```
