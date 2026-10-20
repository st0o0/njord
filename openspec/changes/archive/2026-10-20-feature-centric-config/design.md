## Context

`EnrichmentOptions` is a reflection-built set of bool toggles keyed by property-name strings; `EnrichmentResolution` filters handlers by `ConfigKey` and unions their `Requirements`. `WeatherAlertHandler` runs 14 `IAlertRule`s as one unit. The Config Builder mirrors the toggles in `types.ts` / `enrichment.json` by hand.

## Decisions

1. **`FeatureKind` enum, no magic strings.** Every feature is a `FeatureKind` member. `FeatureDescriptor(FeatureKind Kind, FeatureCategory Category, string Description, bool DefaultEnabled, DomainRequirements Requirements, DomainRequirements Optional)` is the single source of truth. Handlers expose `Feature`; alert rules expose their own descriptor. `Optional` requirements never disable a handler and never add a domain on their own.
2. **Config key mapping.** The configuration layer maps `FeatureKind` to the config key (`Njord:Features:FrostAlert`). Per AGENTS.md ("wire text from explicit mappings, never `ToString()`") this is an extension method with an explicit `switch`, one arm per member, default throws `ArgumentOutOfRangeException`, pinned by a spec over every member; the key text equals the member name. Code outside the config layer never handles key strings.
3. **Keep `WeatherAlertHandler` as one handler.** Its `Feature` is a composite: the handler is active when at least one rule feature is enabled, and its requirements are the union of the enabled rules' requirements. Disabled rules are skipped. Stream shape and previous-state logic are unchanged.
4. **`FeatureOptions` is dictionary-backed** (`Dictionary<FeatureKind,bool>` bound from `Njord:Features`, enum keys parsed by the binder), seeded from the registered descriptors' defaults, so a new feature needs one enum member and one descriptor, no new property and no reflection. `FeatureOptionsValidator` rejects unknown keys and any `FeatureKind` without a registered descriptor; the architecture spec pins unique kinds across handlers and rules.
5. **Migration at configuration binding time**, in the host: read `Njord:Enrichments:*`; `WeatherAlerts` -> 14 alert kinds, others by identical name; explicit `Njord:Features` entries override; one warning lists legacy keys. Allowed because this is a breaking 0.x release (`refactor!:`); the mapper is the only compatibility code.
6. **`FeatureResolution`** is built from registered descriptors plus `FeatureOptions`: `AllFeatures`, `EnabledFeatures`, `RequiredParams`, `RequiredDomains`, `CostOf(feature)`. `CostOf` is the marginal monthly calls of enabling the feature given the other enabled features (0 when every required domain is already active), computed with `BudgetCalculator` so it equals the startup validation.
7. **Cost attribution caveat.** Weather is requested with a fixed variable list, so weather-only features cost +0 once Weather is active; the first feature needing Weather pays the full weather weight. Per-variable-bucket costs only apply to `IncludeSolar`; the builder shows numbers from the real formula.
8. **Builder consumes generated data.** `docs/data/enrichment.json` is replaced by `docs/data/features.json`, exported from the descriptors (kind name, category, description, default, required domains) so the builder cannot drift; `budget.ts` mirrors `BudgetCalculator` and computes marginal cost.

## Future capabilities enabled (not in scope)

- **Multi-model entities.** The ensemble API's `models` parameter (20+ models: ECMWF IFS, DWD ICON, GFS, ...) allows fetching several models in parallel, each exposed as a separate data stream and mapped by ha-njord to its own weather entity (`weather.njord_ecmwf_berlin`, `weather.njord_icon_berlin`). A `ModelComparison` feature kind could request the extra models; its cost (calls multiply per model) would flow through `FeatureResolution.CostOf` unchanged. Requires wire/proto and ha-njord changes, hence a separate change.
- **Budget sensor.** ha-njord exposes `sensor.njord_api_budget` (used and remaining calls) fed from the existing budget data; per-feature cost makes the number explainable. Separate change.

## Risks / Trade-offs

- Config break for operators using `Enrichments`: mitigated by migration plus warning.
- Composite feature for the alert handler is slightly unusual: accepted to avoid splitting stream/previous-state logic.
- A central `FeatureKind` enum means every new feature touches Core; accepted for type safety (matches the "enum with explicit mapping" convention).

## Open Questions

- Whether `Optional` requirements should surface in the builder (default: hint only).
