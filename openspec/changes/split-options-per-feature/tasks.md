Test conventions for all groups: `Spec` suffix, `sealed` class, BDD-style method names.
Actor and async specs carry `[Fact(Timeout = 5000)]`, or `TestTimeouts.Hosted` when they
start a hosted actor system. Pure synchronous specs use a plain `[Fact]`.

## 1. Inventory and classification

- [x] 1.1 List every property of `src/Njord.Core/Configuration/NjordOptions.cs`, `EnrichmentOptions.cs`, `WritableNjordOptions.cs`, and every reader of each property; record the result in this change's design.md under "Context"
- [x] 1.2 Create the host test project `src/Njord.Tests/Njord.Tests.csproj` (it does not exist yet; only stale bin/obj folders are there): executable xUnit v3 project referencing `src/Njord/Njord.csproj` and `Njord.Tests.Shared`, added to `src/Njord.slnx`, set up like the other test projects; delete the stale bin/obj folder contents first, and remove the stale `Njord.Domain.Tests`/`Njord.Egress.Tests` leftovers (bin/obj only, untracked)
- [x] 1.3 Add `ConfigReloadPolicy` (Hot / RestartRequired per options type, plus per property for `NjordOptions`) in `src/Njord.Core/Configuration/ConfigReloadPolicy.cs` with `src/Njord.Core.Tests/Configuration/ConfigReloadPolicySpec.cs` asserting every bound options type is classified

## 2. One options type per section

- [x] 2.1 Add `PollingOptions` (PollInterval, BudgetOverride; section `Njord`) with its validator (budget projection via `BudgetCalculator`) in `src/Njord.Core/Configuration/Polling/`; spec that `Njord__PollInterval` still binds
- [x] 2.2 Move `AlertOptions`, `IndexOptions`, `IndexPreferences`, `LocationIndexOverride` from `src/Njord.Compute/Configuration/` to `src/Njord.Core/Configuration/<Area>/`; give `ConsensusOptions`, `AlertOptions`, `IndexOptions`, `TrendOptions`, `DerivedOptions` a `SectionName` and one validator each (re-target `ConsensusOptionsValidator` and `IndexOptionsValidator` from `EnrichmentOptions`); move the validator specs in `src/Njord.Core.Tests/Configuration/` accordingly
- [x] 2.3 Give `MqttOptions` its own validator (the MQTT host check moves out of `NjordOptionsValidator`); spec in `src/Njord.Core.Tests/Configuration/`
- [x] 2.4 Remove `Mqtt`, `Enrichment`, `PollInterval` and `BudgetOverride` from `NjordOptions`; delete `EnrichmentOptions`; update `NjordOptionsValidator` and its spec
- [x] 2.5 Register each options type once, in its owning setup extension (`src/Njord.Pipeline/Configuration/PipelineSetupContainer.cs` for consensus and polling, feature `ServiceCollectionExtensions` for alerts/derived/trends/indices, `src/Njord.Mqtt/Configuration/MqttSetupContainer.cs`, `src/Njord.SensorHub/Configuration/SensorSetupContainer.cs`); remove the bindings from `src/Njord/Configuration/AnalysisSetupContainer.cs` and `CoreSetupContainer.cs`
- [x] 2.6 Add `src/Njord.Tests/Configuration/OptionsBindingSpec.cs`: build the host configuration from a sample config and assert every options type resolves, validates, and that no `IOptions<EnrichmentOptions>` exists

## 3. Settings records for Compute

- [x] 3.1 Add immutable settings records in `src/Njord.Compute/Settings/` (`AlertSettings` using `SeverityThresholds`, `IndexSettings` with the `(LocationId, IndexKind)` preference map, `ConsensusSettings`, `TrendSettings`, `DerivedSettings`, aggregated as `AnalysisSettings`)
- [x] 3.2 Add `ToSettings()` conversions in `src/Njord.Core/Configuration/<Area>/` with specs (array → `SeverityThresholds`, preference cascade resolved once)
- [x] 3.3 Remove `Microsoft.Extensions.Options` and every `*Options` type from `src/Njord.Compute`; `AlertEvaluator.EvaluateAll` and the computers take settings records; update `src/Njord.Compute.Tests` and `src/Njord.Alerts.Tests`

## 4. Analyzers take settings per computation

- [x] 4.1 Change `AlertAnalyzer`, `DerivedAnalyzer`, `TrendAnalyzer`, `IndexAnalyzer` to read only `Enabled` from `IOptions<T>` and take the current settings in `Compute(...)`; remove `IOptions<NjordOptions>` from them; update `Njord.Alerts.Tests`, `Njord.Derived.Tests`, `Njord.Trends.Tests`, `Njord.Indices.Tests`
- [x] 4.2 Add the analysis-settings side channel to `src/Njord.Pipeline/ForecastPipeline.cs` (seeded with startup settings, `AnalysisSettingsChanged` pushes the latest); spec in `src/Njord.Pipeline.Tests/` showing a settings change applies on the next cycle and not mid-cycle

## 5. Section writer and ConfigManager

- [x] 5.1 Replace `IWritableOptions<NjordOptions>`/`WritableNjordOptions` with `IConfigWriter.UpdateAsync<T>` in `src/Njord.Core/Configuration/`, keeping the override-file mechanics; port the writable-options specs (array replacement, atomic write, concurrency, first-write creation) and add the "invalid update not persisted" spec
- [ ] 5.2 Add `IConfigManager` to `src/Njord.Core/Actors/ActorKeys.cs` and `ConfigManager` in `src/Njord/Configuration/ConfigManager.cs`: hot `OnChange` subscriptions via `Self.Tell`, `AnalysisSettingsChanged` to the pipeline, `PollingChanged` to the scheduler; register it in `NjordActorSystemSetup`
- [ ] 5.3 Add `src/Njord.Tests/Configuration/ConfigManagerSpec.cs` (TestKit): a hot reload reaches pipeline and scheduler probes; a restart-required reload sends nothing
- [ ] 5.4 Make `PollScheduler` handle `PollingChanged` by re-planning its timer with the new interval; inject `IOptions<PollingOptions>` for the start value; spec in `src/Njord.Pipeline.Tests/`

## 6. Narrow injection everywhere else

- [ ] 6.1 `src/Njord.OpenMeteo`: `OpenMeteoClient` and `OpenMeteoGateway` inject `IOptions<NjordOptions>` (scope) only; `GatewayBudgetGate` reads `IOptionsMonitor<PollingOptions>.CurrentValue` per call; update `src/Njord.OpenMeteo.Tests`
- [ ] 6.2 `src/Njord.Mqtt/MqttStatePublisher.cs` drops `NjordOptions` if it only needs MQTT values; otherwise it injects the scope options alone; update `src/Njord.Mqtt.Tests`
- [x] 6.3 `src/Njord.Grpc/AdminGrpcService.cs` writes through `IConfigWriter` per section and adds the restart warning from `ConfigReloadPolicy`; `GetConfig`/`StreamConfig` assemble `NjordConfig` from the section options; update `src/Njord.Grpc.Tests/AdminGrpcServiceSpec.cs` (restart warning for `SetLocations`, none for alert thresholds, rejection leaves the file unchanged)
- [ ] 6.4 Remove `GetRequiredService<IOptions<…>>().Value` from `src/Njord/Configuration/CoreSetupContainer.cs` and `src/Njord.OpenMeteo/Configuration/OpenMeteoSetupContainer.cs` (use factory overloads that resolve at activation); keep it only in `AkkaSetupContainer` and comment why
- [ ] 6.5 Add an ArchUnit rule in `src/Njord.Architecture.Tests/` that no type deriving from an Akka actor base injects `IOptionsMonitor<>`, and that `IOptionsMonitor<>` is used only by `ConfigManager` and `GatewayBudgetGate`

## 7. Validation

- [ ] 7.1 `cd src && dotnet build Njord.slnx` with no new warnings
- [ ] 7.2 `dotnet run --project Njord.Core.Tests/Njord.Core.Tests.csproj --no-build`
- [ ] 7.3 `dotnet run --project Njord.Pipeline.Tests/Njord.Pipeline.Tests.csproj --no-build` and `dotnet run --project Njord.OpenMeteo.Tests/Njord.OpenMeteo.Tests.csproj --no-build`
- [ ] 7.4 `dotnet run --project Njord.Grpc.Tests/Njord.Grpc.Tests.csproj --no-build` and `dotnet run --project Njord.Mqtt.Tests/Njord.Mqtt.Tests.csproj --no-build` (golden masters from `typed-analysis-models` unchanged)
- [ ] 7.5 `dotnet run --project Njord.Tests/Njord.Tests.csproj --no-build` and `dotnet run --project Njord.Architecture.Tests/Njord.Architecture.Tests.csproj --no-build`
- [ ] 7.6 Full loop over all `Njord.*Tests` projects (AGENTS.md "Build & test")
- [ ] 7.7 Run the service once with `src/Njord/appsettings.Development.json` and an existing `data/appsettings.Override.json` to confirm unchanged keys still bind
- [ ] 7.8 From the repo root: `openspec validate split-options-per-feature --strict`
