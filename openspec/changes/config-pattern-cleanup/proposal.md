## Why

The configuration system has accumulated three problems that compound into poor user experience and developer friction: (1) a naming mismatch where C# binds to `Njord:Forecast` but the Config Builder and `appsettings.Development.json` use `Njord:Weather`, causing dev overrides to silently fail; (2) feature config paths nest four levels deep (`Njord:Features:<Category>:<Name>`) producing env vars up to six segments long for a single threshold value, even though all 66 feature names are already unique; (3) array-default logic is copy-pasted as 14 PostConfigure lambdas in `AddFeatures` instead of living with the config classes that own the defaults.

Since we are 0.x, a single breaking change can fix all three at once and establish the pattern for every future config addition.

## What Changes

- **BREAKING**: Rename config section `Njord:Forecast` to `Njord:Weather` (class rename `ForecastOptions` to `WeatherOptions`); all user-facing config paths, env vars, and documentation change accordingly
- **BREAKING**: Flatten feature config paths from `Njord:Features:<Category>:<Name>` to `Njord:Features:<Name>` (66 config classes, features.json keys, Config Builder export/import, docker-compose.example.yml, appsettings.Example.json)
- Introduce `IHasArrayDefaults` interface in `Njord.Core/Configuration/`; config classes with array properties implement it to declare their own defaults; `AddNjordOptions` auto-registers PostConfigure when it detects the interface
- Introduce `AddFeature<TRule, TConfig>()` and `AddFeature<TRule, TConfig, TValidator>()` registration helpers in `Njord.Features`; `AddFeatures` shrinks from ~160 lines to ~66 one-liners
- Remove all manual PostConfigure lambdas for array defaults from `AddFeatures` and `OpenMeteoSetupContainer`
- Update Config Builder (`features.json`, `types.ts`, `serializer.ts`) for flat feature keys and aligned Weather section
- Update AGENTS.md with the consolidated config pattern, templates, and updated options table

## Non-goals

- No new `Njord.Configuration` project; options stay with their owners (FunkArr pattern)
- No changes to feature behavior, thresholds, defaults, or domain requirements
- No changes to polling, budget calculation, or API interaction
- No backward-compatible import of old-format configs in the Config Builder (clean breaking cut)
- No hot-reload or runtime config changes (remains `IOptions<T>`, startup-only)

## Capabilities

### New Capabilities
- `array-default-pattern`: The `IHasArrayDefaults` interface and automatic PostConfigure registration in `AddNjordOptions`, plus the `AddFeature` registration helper

### Modified Capabilities
- `per-feature-config`: Feature config section paths flatten from `Njord:Features:<Category>:<Name>` to `Njord:Features:<Name>`; registration switches to `AddFeature` helper; array-default configs implement `IHasArrayDefaults`
- `service-configuration`: Infrastructure section `Njord:Forecast` renamed to `Njord:Weather`; class renamed `ForecastOptions` to `WeatherOptions`; `WeatherOptions.Models` implements `IHasArrayDefaults`
- `config-builder`: Feature keys flatten from `Category:Name` to `Name`; serializer removes category nesting; `n.Weather` section now matches C# `Njord:Weather`

## Impact

- **User-facing config**: Every feature env var and JSON path loses one segment; `Njord__Forecast__` becomes `Njord__Weather__`; existing user configs break and must be updated
- **C# code**: 66 SectionName constants change; ~14 PostConfigure lambdas removed; `ForecastOptions` renamed throughout; new interface and helpers added
- **Config Builder**: `features.json` keys change; serializer simplified; import/export paths change
- **Documentation**: AGENTS.md options section rewritten; CLAUDE.md config references updated; docker-compose.example.yml and appsettings files updated
- **Tests**: SectionName references in tests updated; all test projects must pass
- **API budget**: No impact (no polling or fetch changes)
