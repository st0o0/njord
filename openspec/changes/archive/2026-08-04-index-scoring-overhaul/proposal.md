## Why

The index scoring system has three problems: (1) the Outdoor score ignores humidity entirely and treats windstill conditions as ideal, producing dangerously misleading scores on hot, humid, windless days (33°C/85% humidity/no wind → score ~70 instead of ~20); (2) HDD/CDD degree-day metrics are building-energy concepts misplaced among lifestyle activity scores — they clutter the index device with sensors that belong in the Energy enrichment feature (which already has its own `HeatingBaseTemp`); (3) all scoring thresholds and curves are hardcoded with zero user configurability beyond three niche values (`HeatingBaseTemp`, `CoolingBaseTemp`, `IndoorTemp`), meaning users cannot tune scores to their personal comfort preferences or local climate.

## What Changes

- **Remove HDD/CDD from indices** — delete `HeatingDegreeDays`/`CoolingDegreeDays` from `IndexScorer`, remove `Hdd`/`Cdd` properties from `IndexResult`, remove their MQTT discovery components and state payload fields, and drop `HeatingBaseTemp`/`CoolingBaseTemp` from `IndexOptions`. **BREAKING**: the `hdd` and `cdd` sensors disappear from the indices device; any HA automations referencing them break. Acceptable because njord is pre-release.
- **Fix Outdoor score formula** — add humidity as a scoring factor; replace the monotonic wind curve (less wind = always better) with a bell-curve where light breeze (2–4 m/s) is ideal and windstill conditions are penalized, especially in combination with heat and humidity.
- **Introduce cascading index preferences** — a layered configuration system where users can tune sensitivity multipliers and ideal-point parameters at three levels: global defaults → per-score overrides → per-location overrides. Resolution cascades from most specific to least specific, with hardcoded defaults as ultimate fallback.
  - **Sensitivity multipliers** (universal, apply across scores): `HeatSensitivity`, `HumiditySensitivity`, `WindSensitivity`, `RainSensitivity` — scale how aggressively the penalty terms grow. Default 1.0 = current behavior.
  - **Ideal-point parameters** (score-specific): `IdealOutdoorTemp` (Outdoor), `IdealTempLow`/`IdealTempHigh` (Running, Cycling), `MinTemp`/`IdealWindLow`/`IdealWindHigh` (BBQ), `IndoorTemp` (Ventilation).

## Non-goals

- Moving HDD/CDD into the Energy enrichment feature — that's a separate change if wanted.
- Adding new index types (e.g. hiking, swimming, allergy).
- Per-hour index scores or multi-day forecast scores — the 24h aggregation stays.
- Changing the envelope computation approach (pessimistic/optimistic bounds).
- Changing which weather parameters Open-Meteo is queried for.

## Capabilities

### New Capabilities
- `index-preferences`: Cascading configuration model for index scoring — global defaults, per-score overrides, per-location overrides, resolution logic, and validation. Covers sensitivity multipliers, ideal-point parameters, and the `ResolvedIndexPreferences` type that `IndexScorer` consumes.

### Modified Capabilities
- `activity-indices`: Remove HDD/CDD (scoring methods, `IndexResult` properties, discovery, state payload, config options). Fix Outdoor score to include humidity and a bell-curve wind model. Thread resolved preferences through all scoring methods.

## Impact

- **Domain** (`IndexScorer`, `IndexResult`): scoring method signatures change to accept preferences; HDD/CDD methods and properties removed.
- **Configuration** (`IndexOptions`): `HeatingBaseTemp`/`CoolingBaseTemp` removed; replaced by nested `Preferences`, `ScoreOverrides`, `LocationOverrides` sections. `IndoorTemp` moves into the preference cascade.
- **MQTT** (`IndexEnrichment`, `DiscoveryPayloadBuilder`, `StatePayloadBuilder`): HDD/CDD discovery components and state fields removed. Sensor count per location drops from 38 to 34.
- **gRPC** (`EnrichmentProtoMapper`): HDD/CDD mapping removed.
- **Tests**: scorer specs, discovery specs, serialization verified files all need updating.
- **Wire format**: `IndexResult` JSON loses `hdd`/`cdd` fields — persistence DTOs need version bump.
- **No API budget impact** — no change to polling or Open-Meteo requests.
