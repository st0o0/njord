## Context

njord has a runtime configuration management system built around:
- **ConfigManager** — an actor that subscribes to `IOptionsMonitor<T>` changes, classifies settings as Hot or RestartRequired, and forwards hot changes to pipeline actors.
- **ConfigWriter** — writes `data/appsettings.Override.json` atomically, loaded as the highest-priority configuration source.
- **AdminGrpcService** — 6 RPCs for runtime CRUD (GetConfig, StreamConfig, SetLocations, SetSettings, SetEnrichment, SetBudget).
- **ConfigReloadPolicy** — classifies each options type as Hot or RestartRequired.
- **AnalysisConfigView** — holds `IOptionsMonitor<T>` for all analysis feature options.

This system adds ~20 production files and ~14 test files for a capability that becomes unnecessary in the new architecture: configuration will be static, changes require a container restart (seconds for Docker).

## Goals / Non-Goals

**Goals:**
- Remove all runtime config mutation infrastructure (ConfigManager, ConfigWriter, AdminService, override file mechanism).
- Simplify the pipeline: settings are constructor-injected from `IOptions<T>`, no side-channel.
- Simplify architecture rules: no Hot/RestartRequired distinction, no `IOptionsMonitor` usage rules for actors.
- Keep read-only infrastructure intact: `ConfigView`, options classes, validators, `AddNjordOptions<T>`.

**Non-Goals:**
- Changing the options classes themselves or their property-initializer defaults.
- Removing the SensorHub, Consensus, Derived, or other features (separate changes).
- Redesigning the gRPC API beyond removing AdminService.
- Changing how `OpsService.GetStatus` works (it reads from actors, not ConfigManager).

## Decisions

### D1: All options become IOptions (no monitors)

**Choice**: Replace all `IOptionsMonitor<T>` usage with `IOptions<T>`. Remove `ConfigReloadPolicy` entirely.

**Rationale**: Without a runtime mutation path, monitors serve no purpose. `IOptions<T>` is simpler and captures intent: the value is fixed for the process lifetime.

**Alternative considered**: Keep `IOptionsMonitor<T>` for future extensibility. Rejected — YAGNI, and it complicates the architecture rules without a consumer.

**Impact on ConfigView**: `ConfigView` currently reads from `IOptionsMonitor<NjordOptions>` and `IOptionsMonitor<PollingOptions>` so PollManager sees hot poll-interval changes. Change it to `IOptions<T>`. PollManager already reads ConfigView at startup; without hot-reload it simply keeps the startup values.

### D2: Pipeline reads settings at construction, no side-channel

**Choice**: `ForecastPipeline` receives analysis settings (alert thresholds, consensus params, etc.) as constructor parameters resolved from `IOptions<T>`. The `Source.ActorRef` + `RepeatPrevious` settings side-channel is removed. The `ZipWith(settings, sensors)` becomes `ZipWith(sensors)` (sensor removal is the next change).

**Rationale**: Without ConfigManager, there is no source of `AnalysisSettingsChanged` messages. The side-channel would always replay the same startup value.

**Alternative considered**: Keep the side-channel with startup-only values for structural consistency. Rejected — dead code is dead code.

### D3: Remove AdminService entirely, keep OpsService and WeatherService

**Choice**: Delete `AdminGrpcService`, its proto, and all request/response mappings. `OpsService` (GetStatus, GetTargets, TriggerPoll) and `WeatherService` (GetCatalog, GetForecast, GetEnrichments, StreamForecasts, StreamEnrichments) remain unchanged.

**Rationale**: GetConfig/StreamConfig could theoretically remain as read-only RPCs, but they return `EffectiveConfig` which requires `AnalysisConfigView` and `EffectiveConfigMappings` — infrastructure we want to remove. `OpsService.GetStatus` already provides the operational view (budget, poll health).

**Alternative considered**: Keep GetConfig as read-only. Rejected — it pulls in too many dependencies for marginal value. Operators can inspect configuration via `docker inspect` or environment variable dumps.

### D4: Remove override file and its configuration source

**Choice**: Delete `ConfigWriter`, `IConfigWriter`, `ConfigUpdateHandler`, `ResilientJsonConfigurationSource`, `ConfigOverrideFile`, `LegacyConfigKeys`. The configuration source chain becomes: `appsettings.json` → `appsettings.{Environment}.json` → environment variables.

**Rationale**: Without runtime mutations, there is nothing to persist. The legacy key migration (`LegacyConfigKeys`) was for the 0.4→0.5 transition and is no longer needed.

### D5: QueryCatalog stays, QueryConfig simplifies

**Choice**: Keep `QueryCatalog` and its handler (catalog is useful for gRPC `GetCatalog`). Simplify or remove `QueryConfig` — if nothing asks the ConfigManager for the effective config, the message type can be removed. If `OpsService` needs a config summary, it can read `IOptions<T>` directly.

### D6: OptionsAccessArchitectureSpec simplifies

**Choice**: Remove the Hot/RestartRequired classification and the allow-list. The new rule is simpler: all options are read via `IOptions<T>` or `ConfigView`. Actors SHALL NOT inject `IOptionsMonitor<T>` (the existing `ConfigurationArchitectureSpec` rule stays, but becomes unconditional — no allow-list exceptions).

## Risks / Trade-offs

- **[Operators lose runtime config changes]** → Acceptable for Docker containers where restart takes seconds. Document the change in release notes. ha-njord config UI removal is a breaking change for users who relied on it.
- **[ha-njord must be updated in lockstep]** → ha-njord's AdminService client calls will fail against the new njord. Coordinate releases or version-gate the proto.
- **[ConfigSetupContainer partial removal]** → The container also registers options and validators. Keep that registration, only remove the ConfigManager actor registration and ConfigWriter service registration.

## Migration Plan

1. Remove ConfigManager actor, IConfigManager marker, actor registration.
2. Remove ConfigWriter, IConfigWriter, ConfigUpdateHandler, ResilientJsonConfigurationSource, ConfigOverrideFile, LegacyConfigKeys.
3. Remove AdminGrpcService, admin.proto, ConfigRequestMappings, ConfigResponseMappings.
4. Remove EffectiveConfigMappings, ConfigUpdateMappings, AnalysisConfigView, ConfigReloadPolicy.
5. Remove Messages/Config/ update commands and response records (keep QueryCatalog).
6. Update ForecastPipeline: remove ConfigManager ask, remove settings side-channel, inject settings from IOptions at construction.
7. Update ConfigView: IOptionsMonitor → IOptions.
8. Update ConfigSetupContainer: keep options registration, remove actor and writer registration.
9. Update architecture tests: simplify OptionsAccessArchitectureSpec, remove ConfigWriter from ZoneArchitectureSpec allow-lists.
10. Remove ~14 test files and test helpers.
11. Update CLAUDE.md/AGENTS.md to reflect the new config model.

## Open Questions

- Should `QueryConfig` message type be kept for a read-only OpsService endpoint, or removed entirely? Leaning toward removal since OpsService.GetStatus covers operational needs.
