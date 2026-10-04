## 1. Shared Test Infrastructure

- [ ] 1.1 Create `src/Njord.Tests.Shared/TestOptionsMonitor.cs` — generic `TestOptionsMonitor<T> : IOptionsMonitor<T>` with `Update(T)` method that fires `OnChange` listeners
- [ ] 1.2 Replace `MutableOptionsMonitor` in `src/Njord.Grpc.Tests/OpsGrpcServiceSpec.cs` with `TestOptionsMonitor<NjordOptions>`
- [ ] 1.3 Replace `MutableOptionsMonitor`/`FakeOptionsMonitor` in `src/Njord.Grpc.Tests/AdminGrpcServiceSpec.cs` with `TestOptionsMonitor<NjordOptions>`
- [ ] 1.4 Replace duplicated options monitor fake in `src/Njord.Mqtt.Tests/` (find the file) with `TestOptionsMonitor<T>`

## 2. Extract MqttOptions

- [ ] 2.1 Add `public const string SectionName = "Njord:Mqtt"` to `src/Njord.Core/Configuration/MqttOptions.cs`; remove the `Mqtt` property from `NjordOptions`
- [ ] 2.2 Update `src/Njord.Mqtt/MqttServiceCollectionExtensions.cs` `AddNjordMqtt()` to accept `IConfiguration`, call `AddOptions<MqttOptions>().Bind(config.GetSection(MqttOptions.SectionName)).ValidateOnStart()`
- [ ] 2.3 Update `MqttConnectionActor` to inject `IOptionsMonitor<MqttOptions>` instead of extracting from `IOptions<NjordOptions>`
- [ ] 2.4 Update `MqttDiscoveryActor`, `MqttStateActor` to inject `IOptions<MqttOptions>` where they use MQTT config
- [ ] 2.5 Update all MQTT test specs to construct with the new options type

## 3. Extract GrpcOptions

- [ ] 3.1 Add `public const string SectionName = "Njord:Grpc"` to `src/Njord.Core/Configuration/GrpcOptions.cs`; remove the `Grpc` property from `NjordOptions`
- [ ] 3.2 Update `src/Njord.Grpc/GrpcServiceCollectionExtensions.cs` `AddNjordGrpc()` to accept `IConfiguration` and register `GrpcOptions`
- [ ] 3.3 Update gRPC services that read `GrpcOptions` to inject `IOptions<GrpcOptions>`
- [ ] 3.4 Update gRPC test specs

## 4. Extract SensorOptions

- [ ] 4.1 Add `public const string SectionName = "Njord:Sensors"` to `src/Njord.Core/Configuration/SensorOptions.cs`; remove from `NjordOptions`
- [ ] 4.2 Move `SensorOptionsValidator` to validate `IValidateOptions<SensorOptions>` instead of `IValidateOptions<NjordOptions>`
- [ ] 4.3 Register `SensorOptions` in `AddNjordPipeline()` or a new `AddNjordSensors()` extension
- [ ] 4.4 Update `SensorHubActor` and `EnrichmentActor` to inject `IOptions<SensorOptions>`
- [ ] 4.5 Update sensor and enrichment test specs

## 5. Extract EnrichmentOptions

- [ ] 5.1 Add `public const string SectionName = "Njord:Enrichment"` to `EnrichmentOptions`; remove from `NjordOptions`
- [ ] 5.2 Move enrichment sub-validators (`ConsensusOptionsValidator`, `HistoryOptionsValidator`, `IndexOptionsValidator`) to validate their respective sub-options under `EnrichmentOptions`
- [ ] 5.3 Register `EnrichmentOptions` in `AddNjordEnrichment()`
- [ ] 5.4 Update enrichment actors/features to inject `IOptions<EnrichmentOptions>`
- [ ] 5.5 Update enrichment test specs

## 6. Extract PersistenceOptions

- [ ] 6.1 Add `public const string SectionName = "Njord:Persistence"` to `PersistenceOptions`; keep `PersistencePath` on `NjordOptions` for the DB file path
- [ ] 6.2 Register `PersistenceOptions` in `NjordActorSystemSetup.ConfigureSystem()` or `NjordServiceSetup`
- [ ] 6.3 Update persistence-dependent code to inject `IOptions<PersistenceOptions>`

## 7. Slim NjordServiceSetup and NjordOptionsValidator

- [ ] 7.1 Remove sub-option registration from `NjordServiceSetup.SetupServices()` — feature libs now handle their own
- [ ] 7.2 Remove sub-option validators from `NjordServiceSetup` registration — feature libs register their own
- [ ] 7.3 Slim `NjordOptionsValidator` to only validate cross-cutting concerns (locations, models, persistence path)
- [ ] 7.4 Update `NjordServiceSetupSpec` and other host tests for the new structure

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
