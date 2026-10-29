## Context

njord has 77 `IConfigSection` implementations (66 feature configs, 11 infrastructure). Feature configs bind to `Njord:Features:<Category>:<Name>` producing deeply nested env vars. A naming mismatch between C# (`Njord:Forecast`) and the Config Builder / dev settings (`Njord:Weather`) causes silent config failures. Array-default logic is scattered across 14 PostConfigure lambdas in `AddFeatures`.

The reference direction is: Persistence <- Core <- Messages <- {Feature libraries} <- Host. Options live with their owner (Core for shared infra, OpenMeteo for domain options, Features for feature configs, Host for persistence).

## Goals / Non-Goals

**Goals:**
- One consistent config pattern for all 77 config classes, documented in AGENTS.md
- Flat feature paths (`Njord:Features:<Name>`) for shorter env vars and simpler JSON
- Naming alignment: `Njord:Weather` everywhere (C#, Builder, dev settings, docs)
- Array-default logic encapsulated in config classes, not registration code
- One-line feature registration via `AddFeature` helper

**Non-Goals:**
- Separate `Njord.Configuration` project (would cause circular dependency with Core)
- Backward-compatible import of old config format in the builder
- Changes to feature behavior, thresholds, or domain requirements
- Hot reload or runtime config mutation

## Decisions

### D1: Rename to `Njord:Weather`, not fix Builder to `Njord:Forecast`

**Choice**: Rename C# `ForecastOptions` to `WeatherOptions` with `SectionName = "Njord:Weather"`.

**Why**: "Weather" is the natural user-facing term. `Njord__Weather__ForecastDays` reads as "weather forecast days" (natural). `Njord__Forecast__ForecastDays` reads as "forecast forecast days" (redundant). The Config Builder and `appsettings.Development.json` already use "Weather". The internal domain enum stays `DomainKind.Forecast` (API-precise terminology).

**Alternative**: Fix Builder/dev settings to use "Forecast". Rejected because it worsens user ergonomics and requires changes in both directions anyway.

### D2: Remove category from feature paths, keep as metadata

**Choice**: `Njord:Features:<Name>` instead of `Njord:Features:<Category>:<Name>`. Category stays as a field in `features.json` for Config Builder UI grouping.

**Why**: All 66 feature names are unique. The category adds one nesting level to every env var and JSON path with zero disambiguation value. The `Features` namespace prefix is still needed to avoid collisions with infrastructure sections (`Njord:Weather`, `Njord:AirQuality`, etc.).

**Alternative**: Remove `Features` too (e.g. `Njord:Frost`). Rejected because it collides with infrastructure section names at the same level.

### D3: IHasArrayDefaults as a separate interface, not on IConfigSection

**Choice**: New `IHasArrayDefaults` interface with `void ApplyDefaults()` (instance method). Config classes opt in by implementing it.

**Why**: Only ~15 of 77 configs need array defaults. Putting it on `IConfigSection` would add a no-op virtual method to 62 classes. A separate interface is explicit about which configs have array-binding workarounds. `AddNjordOptions` uses `typeof(IHasArrayDefaults).IsAssignableFrom(typeof(T))` to auto-register PostConfigure.

**Alternative**: Static virtual method on IConfigSection with default no-op. Rejected because C# static virtual methods cannot call other static abstract members, making it awkward for the typed self-reference needed.

### D4: AddFeature helper in Njord.Features, not in Core

**Choice**: `AddFeature<TRule, TConfig>()` lives in `Njord.Features/ServiceCollectionExtensions.cs`.

**Why**: The helper references `IFeatureRule` (from `Njord.Messages`) and `AddNjordOptions` (from `Njord.Core`). Core cannot reference Messages (wrong direction). Features references both, so the helper belongs there alongside `AddFeatures`.

### D5: Keep `private const string SectionName` pattern

**Choice**: Two-line pattern (`private const` + explicit interface implementation) stays in all config classes.

**Why**: Explicit, grep-friendly, consistent. The const could be referenced by tests or diagnostics in the future. One-liner (`static string IConfigSection.SectionName => "..."`) is shorter but loses the named constant.

### D6: Clean breaking cut for Config Builder

**Choice**: No backward-compatible import of old `Category:Name` keys or `Njord__Features__<Category>__<Name>__` env vars.

**Why**: 0.x allows breaking changes. Dual-format import adds complexity with no long-term value. Users regenerate their config from the builder after updating.

## Risks / Trade-offs

- **[User config breakage]** Every existing user's feature env vars and JSON paths change. -> Mitigation: clear migration notes in release; docker-compose.example.yml is the primary template users copy from.
- **[Rename ripple]** `ForecastOptions` is referenced across multiple projects (OpenMeteo, Polling, tests). -> Mitigation: IDE rename + `dotnet build` catches all references; sequential task ordering.
- **[features.json key change]** Shared Config Builder URLs (hash-encoded state) break. -> Mitigation: acceptable for 0.x; users regenerate.

## Migration Plan

1. Infrastructure first: `IHasArrayDefaults`, updated `AddNjordOptions`, `AddFeature` helper
2. Rename `ForecastOptions` -> `WeatherOptions` with section `Njord:Weather`
3. Flatten all 66 feature config `SectionName` constants, implement `IHasArrayDefaults` where needed
4. Rewrite `AddFeatures` to use `AddFeature` helper, remove PostConfigure lambdas
5. Update Config Builder: `features.json` keys, `serializer.ts`, `types.ts`
6. Update config files: `appsettings.Example.json`, `docker-compose.example.yml`
7. Update AGENTS.md and CLAUDE.md
8. Run all tests, `dotnet format`, verify build

## Open Questions

None remaining; all decisions resolved during exploration.
