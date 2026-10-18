## Why

Compared with FunkArr, njord's options still deviate in four ways. Four unrelated option classes (`NjordOptions`, `PollingOptions`, `OpenMeteoOptions`, `StorageOptions`) bind the same `Njord` section, so each sees the others' keys and operators cannot tell which setting belongs to which concern; FunkArr has one section per concern (`FunkArr:Download`, `FunkArr:Routes`). The binding pattern is repeated by hand in every setup container (bind, collection defaults, validator, `ValidateOnStart`). `ConfigReloadPolicy` already declares which options are hot and which need a restart, but nothing enforces that hot options are read through `IOptionsMonitor` and restart-only options through `IOptions` (FunkArr's rule); `PollingOptions` is read both ways. The compose example repeats defaults in `# Default:` comments with no check against `appsettings.json`.

## What Changes

- **BREAKING** Configuration keys move into one section per concern: `Njord:Polling:{PollInterval,DiscoveryInterval,BudgetOverride}`, `Njord:OpenMeteo:BaseUrl` (was `Njord:OpenMeteoBaseUrl`) and `Njord:Storage:PersistencePath`. `NjordOptions` keeps the forecast scope (`Locations`, `Models`, `Horizons`, `ForecastDays`, `Parameters`) at `Njord`. Environment variables follow (`Njord__Polling__PollInterval`).
- Startup fails with a clear message when an old root-level key is present in configuration or environment; the override file written by earlier versions is migrated in place when it is loaded (the web UI and gRPC wrote `PollInterval` at the root).
- One registration helper `AddNjordOptions<T>` in `Njord.Core` replaces the repeated bind, collection-default, validator and `ValidateOnStart` lines; it uses `Bind(configuration.GetSection(T.SectionName))` like FunkArr.
- Access rule enforced by an architecture spec: options marked hot in `ConfigReloadPolicy` are never injected as `IOptions<T>`; restart-required options are never injected as `IOptionsMonitor<T>` (the `ConfigView`/`ConfigManager` are the allowed monitor consumers). Existing violations are fixed.
- `docker-compose.example.yml` default comments are checked against `appsettings.json` by a spec; the web config builder and docs use the new keys.
- `Njord.Tests.Shared` gets a `TestOptionsMonitor<T>` (as in FunkArr) for specs that need a monitor.

## Capabilities

### New Capabilities
- `options-conventions`: one section per concern, one registration helper, the hot/restart access rule, legacy-key handling and example-file drift checks.

### Modified Capabilities
- `standalone-sub-options`: section ownership and key names.
- `service-configuration`: persistence options section.
- `persistence-provider`: path key location.
- `openmeteo-client`: base URL key.
- `docker-compose-reference`: variable names and checked defaults.
- `config-builder`: import and export of the new keys.

## Impact

- `Njord.Core/Configuration/{Polling,OpenMeteo,Persistence}/*Options.cs` (`SectionName` constants), `Njord/appsettings.json`, `appsettings.Example.json`, `docker-compose.example.yml`, `Njord/Configuration/*SetupContainer.cs`, `ResilientJsonConfigurationSource`, `ConfigWriter` paths, `ConfigManager` write targets, `docs/configuration`, `docs/.vitepress/theme/builder/{serializer,types}.ts`, e2e compose files, architecture specs, tests that set the old keys.
- **BREAKING for operators**: compose files and env vars using the old keys must be renamed; the release notes for 0.5.0 list the mapping. ha-njord is unaffected (gRPC only). No wire or persistence change.
- Commit type `refactor!:`; the single `Release-As: 0.5.0` footer is already present on earlier commits, do not add another.
- Applies after `parameter-registry-redesign` (which touches the options validators) and `extract-actor-mappings`.
