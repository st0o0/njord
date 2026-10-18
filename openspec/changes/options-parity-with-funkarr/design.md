## Context

FunkArr binds one options class per concern to its own section, registers each with `Bind(configuration.GetSection(...))` and `ValidateOnStart`, reads runtime-tunable options through `IOptionsMonitor<T>` and start-time options through `IOptions<T>`, and documents defaults in its compose example. njord already has validators, `ConfigReloadPolicy` (Hot or RestartRequired per type and property), `ConfigView`, and (after `config-defaults-in-appsettings`) defaults in `appsettings.json`. Four option classes still share the `Njord` section.

## Goals / Non-Goals

**Goals:** per-concern sections, one registration helper, an enforced read rule, loud handling of old keys, checked example defaults.

**Non-Goals:** merging `StorageOptions` into `PersistenceOptions` (separate follow-up), changing property names other than `OpenMeteoBaseUrl` to `BaseUrl`, changing validator rules, changing the override-file mechanism or the reload policy values.

## Decisions

1. **Key layout.** `Njord` keeps `Locations`, `Models`, `Horizons`, `ForecastDays`, `Parameters` (the forecast scope, restart-required). New sections: `Njord:Polling` (hot), `Njord:OpenMeteo` (`BaseUrl`, restart-required), `Njord:Storage` (`PersistencePath`, restart-required). `Persistence`, `Mqtt`, `Sensors` and `Enrichment:*` are unchanged. The `BaseUrl` rename drops the redundant prefix now that the section names the concern.
2. **Legacy keys: hard error, plus override-file migration.** There is no read-both fallback (it would keep two spellings alive). `NjordOptionsValidator` checks the five old keys in the root `Njord` section (via `IConfiguration`, supplied as a validator dependency) and fails with `old key -> new key` messages. The override file is the exception, because the product itself wrote old keys into it: `ResilientJsonConfigurationSource` moves those keys to the new sections while loading, logs one warning, and `ConfigWriter` persists the migrated shape on its next write. Migration code is allowed here because this is the breaking release (0.5.0) and it can be removed one release later.
3. **Registration helper.** `AddNjordOptions<T>(configuration)` is generic over `T : class, new()` with a `SectionName` constant read through `ConfigSections.PathOf<T>()`, so no static-abstract interface is needed. It looks up `IValidateOptions<T>` registration by convention (a `TValidator` overload registers it explicitly). Containers shrink to one line per options type.
4. **Access rule as architecture spec.** The spec scans constructor parameters of production types for `IOptions<T>`/`IOptionsMonitor<T>` and checks `T` against `ConfigReloadPolicy.ModeOf(typeof(T))`. Types with mixed property modes (for example `ConsensusOptions`, hot overall but restart-required `Enabled`) count as hot. Allow-list: `ConfigView`, `ConfigManager`, `ConfigWriter` (needs to bind from a monitor for validation), plus setup containers. First task of the apply phase is an audit of the three current `IOptions<PollingOptions>` sites; each is either made a monitor read or removed because the value is only needed at start.
5. **Compose defaults check.** The spec parses `# Default:` comments next to commented variables and compares them to the bound shipped file (durations normalised); list-valued defaults compare to the code default. Variables without a default comment are skipped.
6. **Builder and docs.** `serializer.ts` and `types.ts` emit and read the new keys and flag old ones as outdated; docs and the e2e compose files are updated with a before/after table for the release notes.

## Risks / Trade-offs

- Operators with existing compose files break at start; the failure message gives the exact new key, which is the intended trade-off for a 0.x breaking release.
- The override-file migration adds code that is removed later; it is isolated in one class with its own spec so deletion is trivial.
- The access rule can produce false positives for types read once at startup through a monitor; the allow-list is explicit and reviewed.

## Migration Plan

Order: (1) key moves with legacy detection and override migration, (2) registration helper, (3) access-rule audit and spec, (4) compose default check, (5) builder, docs and e2e files, release-note table. Each step keeps the full suite green; the host options-binding and example-config specs move to the new keys in step 1.
