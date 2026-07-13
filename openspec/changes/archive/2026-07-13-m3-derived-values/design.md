## Context

The enrichment pipeline (M0) established the `EnrichmentActor` → `BroadcastHub<ModelSnapshot>` → consumer stream architecture. M1 added the consensus consumer, M2 added the alert consumer. Both follow the same pattern: pure static computation class, typed result record with `ToMqttMessages`, delta publishing, and consumer-stream materialization gated by config.

M3 adds a derived-values consumer following the identical pattern. The derived values are all computable from existing hourly parameters (`temperature_2m`, `dew_point_2m`, `wind_speed_10m`, `weather_code`, `pressure_msl`, `surface_pressure`, `sunshine_duration`, `is_day`) — no new API data needed.

## Goals / Non-Goals

**Goals:**
- Pure, independently testable computation functions for all seven derived quantities
- One MQTT device per location with one sensor per (derived-quantity, horizon) for horizon-based values and one sensor per scalar derived value
- Same consumer-stream wiring pattern as consensus and alerts
- WMO weather code table as a reusable lookup

**Non-Goals:**
- Configurable comfort thresholds (hardcoded meteorological standards for v1)
- Heat index / WBGT (requires wet-bulb data not available from Open-Meteo)
- Altitude-aware inversion detection (needs vertical profiles)
- Per-model derived devices (derived values are computed per-model but published on a single derived device per location using median across models — same philosophy as consensus)

## Decisions

### D1: Single derived device per location (not per model)

**Decision:** One HA device `njord_{location}_derived` per location. Derived values are computed from the consensus (median across models) rather than per-model, because derived quantities like Beaufort or comfort level are most useful as a single authoritative value.

**Alternative rejected:** Per-model derived devices — would multiply the sensor count (7 derived × 6 horizons × 8 models = 336 per location) for minimal user value. Users who want per-model wind speed already have the raw model devices.

### D2: Horizon-based vs scalar derived values

**Decision:** Split derived values into two categories:
- **Horizon-based** (one value per horizon): Beaufort, wind chill, dew-point comfort, WMO description — these vary by forecast hour and follow the same horizon grid as consensus.
- **Scalar per snapshot** (one value per location): diurnal amplitude, sunshine percentage, inversion detection — these summarize a 24h window or use the full series.

The derived device state topic carries all horizon-based values in one JSON per horizon (like consensus), plus a single `derived/meta` topic for the scalar values.

### D3: DerivedComputer as pure static class

**Decision:** `DerivedComputer` is a static class with pure functions, identical pattern to `ConsensusComputer` and `AlertEvaluator`.

```
DerivedComputer.Beaufort(double? windSpeedMs) → int?
DerivedComputer.WindChill(double? tempC, double? windSpeedMs) → double?
DerivedComputer.DewPointComfort(double? dewPointC) → string?
DerivedComputer.DiurnalAmplitude(ForecastSeries, DateTimeOffset now) → double?
DerivedComputer.SunshinePercent(ForecastSeries, DateTimeOffset now) → double?
DerivedComputer.WmoDescription(int? weatherCode) → string?
DerivedComputer.InversionDetected(double? pressureMsl, double? surfacePressure, double? temp2m, double? dewPoint) → bool?
```

Each function takes the minimal inputs it needs. `DerivedResult.Compute` orchestrates them over the snapshot, extracting values from the consensus or median of available models.

### D4: WMO 4677 table as static dictionary

**Decision:** The WMO weather code → description mapping is a `static IReadOnlyDictionary<int, string>` in `DerivedComputer`. The table covers codes 0–99 (standard WMO 4677 present weather). Open-Meteo returns these codes directly in `weather_code`.

**Alternative rejected:** Enum with description attributes — over-engineering for a lookup table that never changes and is consumed as a string.

### D5: Wind chill uses the North American formula

**Decision:** Wind chill = 13.12 + 0.6215T − 11.37V^0.16 + 0.3965TV^0.16, where T is °C and V is km/h. Applicable when T ≤ 10 °C and V > 4.8 km/h. Outside these bounds, wind chill is `null` (not meaningful). Wind speed from Open-Meteo is in m/s (enforced by `wind_speed_unit=ms`), so convert to km/h internally (×3.6).

### D6: Inversion heuristic from surface pressure differential

**Decision:** Inversion is detected heuristically: when `pressure_msl − surface_pressure` exceeds a threshold (indicating the station is significantly below the pressure-reduction altitude) AND `dew_point_2m` is close to `temperature_2m` (fog/mist conditions). This is a coarse proxy — proper inversions need upper-air data.

The heuristic: `inversion = (pressure_msl − surface_pressure > 3 hPa) AND (temperature_2m − dew_point_2m < 3 °C)`. Published as a boolean sensor.

### D7: Topic scheme for derived values

**Decision:**
```
njord/{location}/derived/{horizon}    Horizon-based derived values (JSON)
njord/{location}/derived/meta         Scalar derived values (JSON)
```

Device id: `njord_{location}_derived`. Discovery follows the alert-device pattern: one device payload with sensor components for each derived quantity at each horizon, plus the scalar sensors.

## Risks / Trade-offs

**[WMO codes not returned by all models]** → Some models may not include `weather_code`. The derived computation handles `null` gracefully — WMO description is `null` when the code is missing.

**[Wind chill only useful in cold weather]** → By definition, wind chill is `null` above 10 °C. The sensor will show `unavailable` in summer. This is correct behavior — document it in the HA entity name.

**[Inversion heuristic is coarse]** → The pressure-differential approach has false positives in mountainous terrain where the station altitude creates a natural gap. Acceptable for a "hint" sensor, not for safety-critical use.

**[Sunshine percentage depends on is_day or sunshine_duration]** → Not all models provide `sunshine_duration`. Fallback: use `is_day` to determine daylight hours and `cloud_cover` as a sunshine proxy. The computation gracefully degrades.
