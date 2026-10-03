## Context

The alert system evaluates consensus forecast data against thresholds and produces typed `Alert` records published as binary sensors to Home Assistant via MQTT. Currently 9 alert types exist, four of which (Frost, Storm, Fog, PressureDrop) are limited to Yellow severity. Five new alert types are needed for safety-relevant weather phenomena, and the four limited types need full severity graduation.

All required forecast parameters are already in `ParameterRegistry` and requested from the API. The `AlertEvaluator` is a static class with one pure function per alert type — no actor, no state, no side effects.

## Goals / Non-Goals

**Goals:**
- Add 5 new `AlertType` enum values with evaluator functions following existing patterns
- Graduate Frost and Storm to tiered `double[]` thresholds (matching Heat's pattern)
- Graduate Fog and PressureDrop with additional Orange thresholds
- Keep all evaluators as pure functions on `ConsensusSnapshot`
- Maintain backward compatibility in persistence (old snapshots deserialize safely)

**Non-Goals:**
- No changes to polling, API parameters, or request budget
- No changes to enrichment pipeline topology or actor hierarchy
- No new MQTT devices — new alerts are components on the existing alerts device
- No changes to gRPC proto definitions (the existing `Alert` message already carries type/severity/confidence/attributes dynamically)

## Decisions

### 1. Tiered thresholds as `double[]` for all severity-graduated alerts

**Decision**: Use `double[]` consistently for any alert with Yellow/Orange/Red graduation.

**Rationale**: Heat already uses this pattern (`HeatThresholds = [30, 35, 40]`). Frost and Storm follow the same model. New alerts (WindChill, Visibility, TropicalNight, Humidity) all use `double[]`. This gives users full control and keeps the evaluator logic uniform: iterate thresholds high-to-low, first match wins.

**Alternative considered**: Separate named properties (`FrostOrangeThreshold`, `FrostRedThreshold`). Rejected — more config surface, less flexible, inconsistent with Heat.

### 2. Fog and PressureDrop get Orange but not Red

**Decision**: Cap Fog at Orange (persistent fog >= N hours), PressureDrop at Orange (>= severe threshold hPa). No Red for either.

**Rationale**: Fog is uncomfortable/hazardous for driving but rarely life-threatening on its own — Visibility covers the danger axis. PressureDrop is an indicator of incoming weather, not the danger itself — Storm and Thunderstorm cover the consequences. Red should mean "immediate safety risk" and neither qualifies independently.

### 3. Ice evaluator uses `rain` parameter, not `precipitation`

**Decision**: Ice/glatteis detection requires `rain > 0` specifically, not just any precipitation. Snow at -2 C is not ice risk.

**Rationale**: `precipitation` includes snow and showers. Freezing rain (the actual ice risk) requires liquid precipitation that freezes on contact. The `rain` parameter (already in ParameterRegistry, currently unused by alerts) isolates this. Soil temperature (`soil_temperature_0cm`) is a severity escalator: if the ground is already frozen, rain freezes immediately on contact.

### 4. TropicalNight uses `is_day` to identify night hours

**Decision**: Night hours are determined by `is_day == 0` from consensus, not by fixed clock hours.

**Rationale**: Sunrise/sunset varies by season and latitude. Using `is_day` from the API gives location-correct night windows. The same approach is already used by `IndexComputer` for day/night slicing.

### 5. Humidity alert uses dewpoint, not relative humidity

**Decision**: The Humidity alert triggers on `dew_point_2m` thresholds, not `relative_humidity_2m`.

**Rationale**: Dewpoint is an absolute measure of moisture — 21 C dewpoint feels oppressive regardless of air temperature. Relative humidity is temperature-dependent (70% at 35 C is very different from 70% at 15 C). Meteorological services (NWS, DWD) use dewpoint for discomfort thresholds.

### 6. Breaking config changes: rename with no fallback

**Decision**: `FrostThreshold` becomes `FrostThresholds` (double[]), `StormGustThreshold` becomes `StormGustThresholds` (double[]). No backward-compatibility shim.

**Rationale**: njord is pre-v1 (0.3.0). Users with custom config will get a clear startup validation error pointing them to the new keys. A compatibility layer adds code that would be removed at v1 anyway.

### 7. New alert types produce `Alert.None` when parameters are missing

**Decision**: If a required parameter (e.g., `visibility`, `rain`, `soil_temperature_0cm`) is not in the resolved parameter set, the evaluator returns `Alert.None` for that type.

**Rationale**: This follows the existing pattern — every evaluator starts with a null check on its required parameters. Users who don't request the Soil parameter group won't get Ice severity escalation to Red, but will still get Yellow/Orange from temperature + rain alone.

## Risks / Trade-offs

**[Risk] Ice alert false positives near 2 C** — Rain at 1.5 C might not freeze, depending on surface conditions.
→ Mitigation: Yellow at 0-2 C is deliberately cautious ("be aware"); Orange/Red require temp <= 0 C and/or confirmed soil frost. Users can adjust `IceThreshold`.

**[Risk] Visibility parameter availability** — Not all Open-Meteo models provide `visibility`.
→ Mitigation: Same graceful degradation as all other alerts — if consensus has no visibility data, the alert returns None. The parameter is in the Weather group which is always requested.

**[Risk] Persistence DTO growth** — Adding 5 alert types to `EnrichmentSnapshotDtos` increases snapshot size.
→ Mitigation: Marginal — each Alert is ~100 bytes JSON. With 14 instead of 9, that's ~500 bytes more per snapshot. Extend-only: old snapshots missing new types produce Alert.None on recovery.

**[Risk] Discovery payload size** — 14 binary_sensor components instead of 9.
→ Mitigation: Discovery is a retained message sent once (startup + HA birth). Size increase is ~1 KB. Well within MQTT limits.

## Open Questions

None — all decisions are grounded in existing patterns and the exploration conversation.
