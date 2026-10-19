## Why

njord is being redesigned from a multi-model consensus system to a lean Open-Meteo proxy with domain-specific enrichment. The runtime config management system (ConfigManager actor, ConfigWriter, gRPC AdminService, override files, hot-reload) adds substantial complexity for a capability that becomes unnecessary: in the new architecture, config is static and changes require a container restart (seconds for a Docker container). Removing it first clears the path for the pipeline and polling redesign that follows.

## What Changes

- **BREAKING**: Remove gRPC `AdminService` — all 6 RPCs (`GetConfig`, `StreamConfig`, `SetLocations`, `SetSettings`, `SetEnrichment`, `SetBudget`) are removed. ha-njord must remove its config UI.
- **BREAKING**: Remove runtime config override file mechanism (`data/appsettings.Override.json`). Configuration is now exclusively `appsettings.json` + environment variables + optional file mounts.
- Remove `ConfigManager` actor, `IConfigManager` marker, `ConfigSetupContainer` actor registration.
- Remove `ConfigWriter`, `IConfigWriter`, `ConfigUpdateHandler`, `ResilientJsonConfigurationSource`.
- Remove `ConfigOverrideFile`, `LegacyConfigKeys` (0.5.0 migration code).
- Remove `ConfigReloadPolicy` and the Hot/RestartRequired distinction — all options become restart-required.
- Remove `AnalysisConfigView` (IOptionsMonitor holder for analysis features, only consumed by ConfigManager).
- Remove `EffectiveConfigMappings`, `ConfigUpdateMappings`, `ConfigRequestMappings`, `ConfigResponseMappings`.
- Remove `Messages/Config/` update commands and response records: `UpdateLocations`, `UpdateSettings`, `UpdateAnalysisConfig`, `UpdateBudget`, `AnalysisSettings`, `EffectiveConfig`, `EnrichmentSection`, `ConfigIssue`.
- Remove ~14 test files and test helpers (`ConfigManagerSpec`, `ConfigWriterSpec`, `ConfigReloadPolicySpec`, `AdminGrpcServiceSpec`, `FakeConfigManager`, `TestAnalysisConfigView`, etc.).
- Simplify `ForecastPipeline`: remove `QueryAnalysisSettings` ask to ConfigManager on PreStart, remove `AnalysisSettings` ZipWith side-channel. Read analysis settings from `IOptions<T>` at construction.
- Simplify architecture tests: remove Hot/Monitor enforcement from `OptionsAccessArchitectureSpec`, remove ConfigWriter allow-list entries from `ZoneArchitectureSpec`.
- Keep: `ConfigView` (read-only scope + polling), all options classes and validators, `AddNjordOptions<T>`, `QueryCatalog`.

## Non-goals

- Removing sensor-related config or the SensorHub — that is a separate change (`remove-sensor-hub`).
- Changing the options classes themselves or their validators — those define the schema and stay.
- Redesigning the pipeline or polling — this change only removes the config management actor and its gRPC surface.
- API budget impact: no change to polling behavior, zero API-budget effect.

## Capabilities

### New Capabilities

_(none — this is a removal change)_

### Modified Capabilities

- `options-hot-reload`: Hot-reload of analysis settings is removed. All options become restart-required.
- `writable-options`: Runtime config writes via ConfigWriter/override file are removed.
- `config-persistence`: Override file persistence mechanism is removed.
- `grpc-v2-admin-service`: The entire AdminService is removed.
- `service-configuration`: ConfigManager actor, ConfigReloadPolicy, and AnalysisConfigView are removed. Config is static IOptions only.
- `pipeline-composition`: Pipeline no longer asks ConfigManager for settings or has an AnalysisSettings side-channel.

## Impact

- **Njord.Grpc**: AdminGrpcService removed, admin.proto removed, 4 mapping classes removed.
- **Njord.Messages**: `Config/` folder loses update commands and response records (keep QueryCatalog, QueryConfig as read-only).
- **Njord.Core**: `ConfigOverrideFile`, `LegacyConfigKeys`, `IConfigManager` marker removed. `ConfigView` stays.
- **Njord (host)**: ConfigManager, ConfigWriter, ConfigUpdateHandler, ResilientJsonConfigurationSource, ConfigReloadPolicy, AnalysisConfigView, ConfigSetupContainer (partially) removed.
- **Njord.Pipeline**: ForecastPipeline simplified — no ConfigManager dependency, no settings side-channel.
- **Architecture tests**: OptionsAccessArchitectureSpec simplified (no Hot/RestartRequired), ZoneArchitectureSpec updated.
- **ha-njord**: Must remove config UI and AdminService client in a separate PR. Config changes require editing docker-compose env vars and restarting.
- **Test count**: ~14 test files removed, ~3 architecture specs simplified.
