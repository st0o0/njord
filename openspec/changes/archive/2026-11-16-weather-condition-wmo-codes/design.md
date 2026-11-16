## Context

Weather forecast icons in HA are broken: njord translates WMO integer codes into custom hyphenated strings (`mostly-clear`, `freezing-drizzle`, etc.), sends them over gRPC, then ha-njord tries to translate those strings into HA condition constants using `_CONDITION_MAP` -- which expects underscored keys and is missing several entries. Additionally, `is_day` is never sent, so HA cannot distinguish sunny (day) from clear-night (night).

ha-njord already has a correct WMO-to-HA mapper in `condition_mapper.py` with day/night support, but it is dead code.

## Goals / Non-Goals

**Goals:**
- Every WMO weather code produces a valid HA condition with the correct icon
- Nighttime clear skies show the clear-night icon instead of sunny
- Eliminate the fragile string-based condition mapping across the wire boundary
- Use the existing `condition_mapper.py` as the single source of truth

**Non-Goals:**
- Changing the WMO code interpretation (the existing mapper is correct)
- Adding wind_direction-based "windy" conditions (HA handles this separately)
- Supporting daily forecasts

## Decisions

### D1: Send WMO codes as integers, not HA condition strings

**Choice:** njord sends the raw WMO integer `weather_code` and `is_day` boolean; ha-njord maps to HA conditions locally.

**Alternative considered:** njord sends HA-compatible condition strings directly (e.g. `sunny`, `clear-night`). Rejected because it couples njord to HA's condition vocabulary and prevents other consumers from interpreting the data differently.

**Alternative considered:** Remove the condition string entirely. Rejected because the string is useful for logging, diagnostics, and older clients that don't know about the new fields.

### D2: Keep the condition string as a diagnostic label

**Choice:** The existing `condition` string field stays on `WeatherValue` and `WeatherForecastPoint` with its current human-readable values. ha-njord ignores it for icon mapping but it remains available for logging and backward compatibility.

### D3: IsDay defaults to true when missing

**Choice:** When `ForecastPoint.IsDay` is null (data not available), default to `true` (daytime). Daytime icons are the more common and less surprising default. This matches the sunny/clear icon which is what most users expect.

### D4: IsDay is a free API parameter

The Open-Meteo Forecast API serves `is_day` without counting it toward the variables-per-10 budget formula. Adding it to the requirements does not increase call weight.

## Risks / Trade-offs

- **[Wire compatibility]** Older ha-njord clients that don't know about `weather_code`/`is_day` will continue using the `condition` string, which has the same bugs as today. Mitigation: ship njord and ha-njord together (they already version-lock).
- **[Condition string drift]** The `condition` string becomes a secondary field that could drift from what `weather_code` represents. Mitigation: both are derived from the same `ForecastPoint.WeatherCode` in `WeatherRule.MapCondition`, which stays as the label generator.

## Open Questions

None.
