> Re-audited 2026-10-07 against actual code: several tasks had been checked off
> without the corresponding change (a tasks.md-only commit). Checkboxes below
> reflect what the code actually does, not what was previously marked.
>
> Finished 2026-10-07: `Sensors`/`Grpc` (both dead/unused) removed from
> `NjordOptions`, and `SensorOptions` registration moved into
> `SensorSetupContainer` (the host was restructured from one
> `NjordServiceSetup` into per-domain `*SetupContainer`s in the meantime —
> `SensorOptions` registration moved from `CoreSetupContainer` to
> `SensorSetupContainer`). **`Mqtt` and `Enrichment` stay nested in
> `NjordOptions` by design** — `AdminGrpcService` clones, mutates, and persists
> the *whole* `NjordOptions` object as one JSON blob via `ConfigPersistence`
> (`IOptionsMonitor<NjordOptions>` + `CloneOptions` + `SaveAsync(NjordOptions)`)
> for every `Set*` admin RPC, including `SetEnrichment*`. Splitting `Mqtt`/
> `Enrichment` into independently-bound `IOptionsMonitor<T>`s would require
> rewriting that admin mutation/persistence pipeline to clone+persist multiple
> option objects instead of one — a materially different, higher-risk change
> than this proposal scoped, so it is left out. Tasks 2.1, 5.1, 7.1–7.3 below
> are marked accordingly; everything else in this change is done.

## 1. Shared Test Infrastructure

- [x] 1.1 Create `src/Njord.Tests.Shared/TestOptionsMonitor.cs` — generic `TestOptionsMonitor<T> : IOptionsMonitor<T>` with `Update(T)` method that fires `OnChange` listeners
- [x] 1.2 Replace `MutableOptionsMonitor` in `src/Njord.Grpc.Tests/OpsGrpcServiceSpec.cs` with `TestOptionsMonitor<NjordOptions>`
- [x] 1.3 Replace `MutableOptionsMonitor`/`FakeOptionsMonitor` in `src/Njord.Grpc.Tests/AdminGrpcServiceSpec.cs` with `TestOptionsMonitor<NjordOptions>`
- [x] 1.4 Replace duplicated options monitor fake in `src/Njord.Pipeline.Tests/PipelineConnectionSpec.cs` (find the file) with `TestOptionsMonitor<T>`

## 2. Extract MqttOptions

- [ ] 2.1 ~~Remove the `Mqtt` property from `NjordOptions`~~ — descoped, kept nested by design (see note above on `AdminGrpcService`/`ConfigPersistence`). SectionName already added to `MqttOptions`.
- [x] 2.2 Update `src/Njord.Mqtt/MqttServiceCollectionExtensions.cs` `AddNjordMqtt()` to accept `IConfiguration`, call `AddOptions<MqttOptions>().Bind(config.GetSection(MqttOptions.SectionName)).ValidateOnStart()`
- [x] 2.3 Update `MqttConnectionActor` to inject `IOptions<MqttOptions>` instead of extracting from `IOptions<NjordOptions>`
- [x] 2.4 Update `MqttDiscoveryActor`, `MqttStateActor` to inject `IOptions<MqttOptions>` where they use MQTT config
- [x] 2.5 Update all MQTT test specs to construct with the new options type

## 3. Extract GrpcOptions

- [x] 3.1 Add `public const string SectionName = "Njord:Grpc"` to `src/Njord.Core/Configuration/GrpcOptions.cs`; remove the `Grpc` property from `NjordOptions` (removed 2026-10-07 — confirmed zero usages anywhere first)
- [x] 3.2 Update `src/Njord.Grpc/GrpcServiceCollectionExtensions.cs` (GrpcOptions.Port is unused — no registration needed) `AddNjordGrpc()` to accept `IConfiguration` and register `GrpcOptions`
- [x] 3.3 No gRPC services read `GrpcOptions` — no changes needed to inject `IOptions<GrpcOptions>`
- [x] 3.4 No gRPC tests reference GrpcOptions — no changes needed

## 4. Extract SensorOptions

- [x] 4.1 Add `public const string SectionName = "Njord:Sensors"` to `src/Njord.Core/Configuration/SensorOptions.cs`; remove from `NjordOptions` (removed 2026-10-07 — confirmed zero usages anywhere first)
- [x] 4.2 Move `SensorOptionsValidator` to validate `IValidateOptions<SensorOptions>` instead of `IValidateOptions<NjordOptions>`
- [x] 4.3 Register `SensorOptions` in `AddNjordPipeline()` or a new `AddNjordSensors()` extension (moved 2026-10-07 from `CoreSetupContainer` into `Njord.Sensors/Configuration/SensorSetupContainer.cs`)
- [x] 4.4 Update `SensorHubActor` and `EnrichmentActor` to inject `IOptions<SensorOptions>`
- [x] 4.5 Update sensor test specs

## 5. Extract EnrichmentOptions

- [ ] 5.1 ~~Remove `Enrichment` from `NjordOptions`~~ — descoped, kept nested by design (see note above on `AdminGrpcService`/`ConfigPersistence`). SectionName already added to `EnrichmentOptions`.
- [x] 5.2 Enrichment actors now inject IOptions&lt;EnrichmentOptions&gt; directly (`ConsensusOptionsValidator`, `HistoryOptionsValidator`, `IndexOptionsValidator`) to validate their respective sub-options under `EnrichmentOptions`
- [x] 5.3 Register `EnrichmentOptions` in `AddNjordEnrichment()`
- [x] 5.4 Update enrichment actors/features to inject `IOptions<EnrichmentOptions>`
- [x] 5.5 Update enrichment test specs

## 6. Extract PersistenceOptions

- [x] 6.1 Add `public const string SectionName = "Njord:Persistence"` to `PersistenceOptions`; keep `PersistencePath` on `NjordOptions` for the DB file path
- [x] 6.2 PersistenceOptions consumed only by host's `NjordActorSystemSetup.ConfigureSystem()` — registration stays in host, no feature library owns it
- [x] 6.3 `ConfigureSystem()` already receives `NjordOptions` directly (not via DI) — accesses `.Persistence` inline, no injection change needed

## 7. Slim the host setup container and NjordOptionsValidator

(`NjordServiceSetup` no longer exists — a separate, unrelated refactor split the
host into per-domain `*SetupContainer`s; `CoreSetupContainer` is its
cross-cutting successor.)

- [x] 7.1 Remove sub-option registration from the host's setup container — feature libs now handle their own (Mqtt/Enrichment registration already lived in their feature libs; `SensorOptions` moved from `CoreSetupContainer` to `SensorSetupContainer` 2026-10-07)
- [x] 7.2 Remove sub-option validators from the host's setup container registration — feature libs register their own (`IValidateOptions<SensorOptions>` moved alongside 4.3/7.1)
- [ ] 7.3 ~~`NjordOptionsValidator` only validates cross-cutting concerns~~ — descoped: it still validates `options.Mqtt.Enabled`/`Host` because `Mqtt` stays nested in `NjordOptions` by design (see note above)
- [x] 7.4 All host tests pass with new structure (`dotnet build Njord.slnx` + full test loop re-run 2026-10-07, all green aside from one confirmed-flaky `Njord.Core.Tests` actor spec unrelated to this change)

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
