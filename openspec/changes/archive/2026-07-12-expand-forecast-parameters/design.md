## Context

njord currently models weather parameters as a closed C# enum (`WeatherParameter`, 9 values) threaded through a typed `ForecastPoint` record. The DTO, domain, and egress layers all switch on this enum exhaustively. This makes adding a single variable a multi-file code change touching DTOs, domain, egress keys, discovery builder, state builder, and tests.

Open-Meteo's `/v1/forecast` offers ~50 hourly and ~20 daily variables. The service should expose all of them as opt-in configuration without per-variable code changes.

## Goals / Non-Goals

**Goals:**
- All Open-Meteo `/v1/forecast` hourly and daily variables queryable via config
- Group-based parameter selection (Weather, Solar, Soil) with individual overrides
- Budget projection accounts for variable-count-driven API call weight
- Daily forecast values surface as HA sensors with day-offset horizons
- Clean extensibility seam for future endpoint types (air quality, marine)

**Non-Goals:**
- Implementing non-forecast endpoints
- Configuration UI
- Minutely-15 data
- Multi-model response format (njord always queries one model per request)

## Decisions

### 1. Parameter identity: string-keyed registry, not enum

**Choice:** Parameters are identified by their Open-Meteo API name (`string`). A static `ParameterRegistry` class holds `ParameterDef` records with metadata.

**Why not keep the enum?** Adding a variable should be a registry entry (data), not a code path (logic). The enum forces exhaustive switches in 6+ locations. A registry with string keys eliminates that coupling.

**Alternative considered:** Large enum (50+ values) — rejected because it still requires exhaustive switches, makes the DTO layer rigid, and gains nothing over a registry since HA device_class/unit lookups are already table-driven.

```
sealed record ParameterDef(
    string ApiName,          // "temperature_2m"
    string Unit,             // "°C"
    string? DeviceClass,     // "temperature" or null
    string JsonKey,          // "temperature" (egress short name)
    string FriendlyName,     // "Temperature (2m)"
    ParameterGroup Group,    // Weather | Solar | Soil
    ParameterGranularity Granularity  // Hourly | Daily
);
```

### 2. ForecastPoint becomes a thin dictionary wrapper

**Choice:** `ForecastPoint` becomes `record ForecastPoint(DateTimeOffset ValidAt, IReadOnlyDictionary<ParameterDef, double?> Values)`. Accessor: `point[param]` or `point.Get(param)`.

**Why:** The current record has 9 named positional parameters — scaling to 30+ is unreadable and makes construction brittle. A dictionary keyed by `ParameterDef` preserves strong typing (not raw strings) while being data-driven.

**Trade-off:** Loses named-property IDE autocomplete. Acceptable because callers always iterate the active parameter set from config, never hardcode individual parameters.

### 3. Daily forecasts as a separate series on ModelForecast

**Choice:** `ModelForecast` gains `DailySeries` alongside `HourlySeries`. `DailyForecastPoint` has `DateOnly Date` + `IReadOnlyDictionary<ParameterDef, double?>`. Daily parameters are a separate subset in the registry (flagged `Granularity = Daily`).

**Why separate:** Hourly and daily have different time semantics (hour-aligned vs. day-aligned), different horizon semantics (hours vs. day-offsets), and different HA sensor naming. Mixing them in one series would require polymorphic time keys.

### 4. Dynamic DTO deserialization via JsonElement

**Choice:** Replace the typed `OpenMeteoHourly` record with `JsonElement`-based extraction. The client reads `hourly.GetProperty(apiName)` for each active parameter, yielding `List<double?>`.

**Why:** The current DTO has hardcoded `[JsonPropertyName]` fields. With 30-50 dynamic variables, a typed record is impractical. `JsonElement` navigation is allocation-free for the path lookup and integrates with the existing `System.Text.Json` source generator for the envelope.

**Alternative considered:** `Dictionary<string, List<double?>>` as the DTO — rejected because it forces full materialization of all arrays even for unused variables. `JsonElement` lets us pick only what's configured.

### 5. Unit verification adapts to active parameters

**Choice:** The client verifies `hourly_units` for every active parameter that has an expected unit (temperatures → `°C`, wind → `m/s`, pressures → `hPa`). The verification set is derived from the registry, not hardcoded.

### 6. Configuration model: Groups + Extra + Exclude

```json
{
  "Njord": {
    "Parameters": {
      "Groups": ["Weather"],
      "Extra": ["uv_index"],
      "Exclude": ["cape"]
    }
  }
}
```

Resolution: `enabled = Union(groups.SelectMany(g => registry.ByGroup(g))) ∪ Extra \ Exclude`

Validation at startup:
- Unknown group names → fail
- Unknown variable names in Extra/Exclude → fail
- Empty resolved set → fail
- Budget projection uses `ceil(hourlyCount / 10)` as weight multiplier

### 7. HA entity scheme for daily parameters

Daily sensors use day-offset naming instead of hour horizons:
- `unique_id`: `njord_{location}_{model}_{param}_d{offset}` (d0 = today, d1 = tomorrow, ...)
- Day offsets derived from `forecast_days` config (default 4 → d0, d1, d2, d3)
- State topic carries both hourly and daily values in one JSON per device:
  ```json
  {
    "h3": { "temperature": 18.2, "wind_speed": 3.1, ... },
    "h6": { ... },
    "d0": { "temperature_max": 24.1, "sunrise": "05:31", ... },
    "d1": { ... }
  }
  ```

### 8. Endpoint extensibility seam (structural only)

**Choice:** Introduce `enum ForecastSource { Forecast, AirQuality, Marine, Flood }` and a namespace convention in the registry (`forecast:temperature_2m` internally, but the prefix is implicit for the only active source). No interface, no plugin system — just a field on `ParameterDef` that today is always `Forecast`.

**Why minimal:** YAGNI. When a second source arrives, the shape is obvious from the registry field. An interface now would be speculative.

## Risks / Trade-offs

- **[Larger default entity count]** Weather group = ~30 hourly × 6 horizons + ~15 daily × 4 days = 240 sensors/device (up from 54). HA performance is fine at this scale, but users should still `recorder: exclude` njord entities.
  → Mitigation: Document the exclude recommendation; consider an `entity_category: diagnostic` for less-common parameters.

- **[API weight increase]** Default weight rises from 1.0 to 3.0 per call.
  → Mitigation: Budget validator now factors in weight; startup rejects configurations that exceed 80% of monthly budget.

- **[Loss of compile-time exhaustiveness]** No compiler warning if a new parameter lacks an egress mapping.
  → Mitigation: Registry is the single source; egress iterates `activeParameters` from the registry, never assumes. Test that verifies every registry entry has valid metadata.

- **[Daily sunrise/sunset are strings, not doubles]** Open-Meteo returns these as ISO time strings, not numbers.
  → Mitigation: `ParameterDef` gains a `ValueType` discriminator (Numeric | TimeString). The domain holds `object?` for daily values or a union type. Egress formats accordingly.

## Open Questions

1. **Should sunrise/sunset be included at all?** They're not numeric forecasts — they're ephemeris data. Could also be a separate HA `sun` integration concern. Decision: include them — users expect them in a weather device, and HA can show them as sensor text.

2. **Precipitation probability availability:** Not all models provide it (e.g., `icon_d2` does not). Should missing parameters for a specific model show as `unavailable` or be omitted from that device's discovery? Current decision (from spec): static grid → always present, shown as `unavailable` when the model doesn't provide it.
