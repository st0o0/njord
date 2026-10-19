## 1. Remove gRPC AdminService

- [x] 1.1 Delete `src/Njord.Grpc/AdminGrpcService.cs` and its proto (`protos/njord/v2/admin.proto` or the admin service definition within the proto files)
- [x] 1.2 Delete `src/Njord.Grpc/Mapping/ConfigRequestMappings.cs` and `src/Njord.Grpc/Mapping/ConfigResponseMappings.cs`
- [x] 1.3 Delete `src/Njord.Grpc.Tests/AdminGrpcServiceSpec.cs`, `src/Njord.Grpc.Tests/ConfigRequestMappingsSpec.cs`, `src/Njord.Grpc.Tests/ConfigResponseMappingsSpec.cs`, `src/Njord.Grpc.Tests/FakeConfigManager.cs`
- [x] 1.4 Remove AdminService registration from gRPC endpoint mapping (likely in the host or a setup container)
- [x] 1.5 Verify `dotnet build Njord.slnx` compiles without errors

## 2. Remove ConfigManager actor and marker

- [x] 2.1 Delete `src/Njord/Configuration/ConfigManager.cs`
- [x] 2.2 Remove `IConfigManager` marker interface from `src/Njord.Core/Actors/ActorKeys.cs`
- [x] 2.3 Remove ConfigManager actor registration from `src/Njord/Configuration/ConfigSetupContainer.cs` (keep options/validator registration)
- [x] 2.4 Delete `src/Njord.Tests/Configuration/ConfigManagerSpec.cs`
- [x] 2.5 Delete `src/Njord.Tests.Shared/TestAnalysisConfigView.cs` and any `FakeConfigManager` in shared test helpers

## 3. Remove ConfigWriter and override file mechanism

- [x] 3.1 Delete `src/Njord/Configuration/ConfigWriter.cs` and `src/Njord/Configuration/IConfigWriter.cs`
- [x] 3.2 Delete `src/Njord/Configuration/ConfigUpdateHandler.cs`
- [x] 3.3 Delete `src/Njord/Configuration/ResilientJsonConfigurationSource.cs`
- [x] 3.4 Delete `src/Njord.Core/Configuration/ConfigOverrideFile.cs`
- [x] 3.5 Delete `src/Njord.Core/Configuration/LegacyConfigKeys.cs`
- [x] 3.6 Delete test files: `src/Njord.Tests/Configuration/ConfigWriterSpec.cs`, `src/Njord.Tests/Configuration/ResilientJsonConfigurationSourceSpec.cs`, `src/Njord.Core.Tests/Configuration/ConfigOverrideFileSpec.cs`, `src/Njord.Core.Tests/Configuration/LegacyConfigKeysSpec.cs`
- [x] 3.7 Remove override file configuration source registration from Program.cs or the host setup

## 4. Remove ConfigReloadPolicy and AnalysisConfigView

- [x] 4.1 Delete `src/Njord/Configuration/ConfigReloadPolicy.cs`
- [x] 4.2 Delete `src/Njord/Configuration/AnalysisConfigView.cs`
- [x] 4.3 Delete `src/Njord.Tests/Configuration/ConfigReloadPolicySpec.cs`
- [x] 4.4 Delete `src/Njord/Configuration/EffectiveConfigMappings.cs` and `src/Njord/Configuration/ConfigUpdateMappings.cs`
- [x] 4.5 Delete `src/Njord.Tests/Configuration/EffectiveConfigMappingsSpec.cs` and `src/Njord.Tests/Configuration/ConfigUpdateMappingsSpec.cs`

## 5. Remove Messages/Config update commands

- [x] 5.1 Delete update command and response records from `src/Njord.Messages/Config/`: `UpdateLocations`, `UpdateSettings`, `UpdateAnalysisConfig`, `UpdateBudget`, `AnalysisSettings`, `EffectiveConfig`, `EnrichmentSection`, `ConfigIssue`
- [x] 5.2 Keep `QueryCatalog` (used by WeatherGrpcService.GetCatalog) and evaluate whether `QueryConfig` can be removed
- [x] 5.3 Remove `AnalysisSettingsChanged` message if it exists (was used by ConfigManager → Pipeline forwarding)
- [x] 5.4 Remove `RequestConfigStream` and config stream wiring messages from `src/Njord.Core/Actors/StreamWiringMessages.cs` if present
- [x] 5.5 Fix any compilation errors in `src/Njord.Messages.Tests/` from removed types

## 6. Update ForecastPipeline

- [x] 6.1 Remove `QueryAnalysisSettings` ask to `IConfigManager` from `ForecastPipeline.PreStart`
- [x] 6.2 Remove the `AnalysisSettings` side-channel (`Source.ActorRef` + `RepeatPrevious` + `ZipWith` for settings). The pipeline reads analysis settings from constructor-injected `IOptions<T>` values
- [x] 6.3 Remove `AnalysisSettingsChanged` message handler from `ForecastPipeline`
- [x] 6.4 Update `src/Njord.Pipeline.Tests/ForecastPipelineSpec.cs` to remove tests for settings side-channel and ConfigManager interaction
- [x] 6.5 Add or verify a test that the pipeline uses constructor-injected settings for analysis

## 7. Simplify ConfigView and options access

- [x] 7.1 Change `ConfigView` (`src/Njord.Core/Configuration/ConfigView.cs`) from `IOptionsMonitor<T>` to `IOptions<T>` for NjordOptions and PollingOptions
- [x] 7.2 Update `src/Njord.Tests.Shared/TestConfigView.cs` accordingly
- [x] 7.3 Update any remaining `IOptionsMonitor<T>` usages in production code to `IOptions<T>` (scan all projects)
- [x] 7.4 Update `ConfigSetupContainer` to remove ConfigWriter and ConfigManager service registrations while keeping options/validator registration

## 8. Update architecture tests

- [x] 8.1 Simplify `src/Njord.Architecture.Tests/OptionsAccessArchitectureSpec.cs`: remove Hot/RestartRequired classification, remove the monitor allow-list. New rule: no `IOptionsMonitor` in production code (or a minimal allow-list if any legitimate use remains)
- [x] 8.2 Update `src/Njord.Architecture.Tests/ConfigurationArchitectureSpec.cs`: the "actors cannot inject monitors" rule becomes unconditional (no allow-list exceptions needed)
- [x] 8.3 Update `src/Njord.Architecture.Tests/ZoneArchitectureSpec.cs`: remove ConfigWriter from allow-lists
- [x] 8.4 Update `src/Njord.Architecture.Tests/DocumentationArchitectureSpec.cs` if it references removed files or spec names

## 9. Update documentation

- [x] 9.1 Update `CLAUDE.md`: remove ConfigManager, ConfigWriter, AdminService, ConfigReloadPolicy, Hot/RestartRequired references. Simplify the options reading rule
- [x] 9.2 Update `AGENTS.md`: remove ConfigManager, AnalysisConfigView, ConfigWriter mentions from architecture description. Remove config stream wiring from consumer model
- [x] 9.3 Update `docker-compose.example.yml` if it references admin/config endpoints
- [x] 9.4 Update `src/Njord/Configuration/ConfigSetupContainer.cs` doc comments if any reference the removed components

## 10. Validation

- [x] 10.1 `dotnet build Njord.slnx` — full solution compiles
- [x] 10.2 `dotnet run --project Njord.Core.Tests/Njord.Core.Tests.csproj` — ConfigOverrideFile, LegacyConfigKeys tests removed, remaining tests pass
- [x] 10.3 `dotnet run --project Njord.Tests/Njord.Tests.csproj` — ConfigManager, ConfigWriter, ConfigReloadPolicy, EffectiveConfigMappings, ConfigUpdateMappings tests removed, remaining tests pass
- [x] 10.4 `dotnet run --project Njord.Grpc.Tests/Njord.Grpc.Tests.csproj` — AdminGrpcService, config mapping tests removed, remaining tests pass
- [x] 10.5 `dotnet run --project Njord.Pipeline.Tests/Njord.Pipeline.Tests.csproj` — pipeline tests pass without ConfigManager dependency
- [x] 10.6 `dotnet run --project Njord.Messages.Tests/Njord.Messages.Tests.csproj` — message tests pass with removed config types
- [x] 10.7 `dotnet run --project Njord.Architecture.Tests/Njord.Architecture.Tests.csproj` — updated architecture rules pass
- [x] 10.8 Run full test suite loop to verify no cross-project breakage:
  ```bash
  for p in Njord.*Tests; do
    [ "$p" = Njord.Tests.Shared ] && continue
    dotnet run --project "$p/$p.csproj" --no-build
  done
  ```
