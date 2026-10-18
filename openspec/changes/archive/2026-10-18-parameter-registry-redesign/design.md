## Context

`ParameterRegistry` builds a list in `BuildAll()` with `H(...)`/`D(...)` helpers, then re-finds 34 entries by string in a static constructor to fill typed properties. `ParameterDef.DeviceClass` is read only by `OpenMeteoClient.UnitMismatch`; `JsonKey` only by the MQTT mappings and `TopicScheme`. `Resolve` parses configuration inside the domain project. 426 call sites use `ParameterRegistry.<Property>`.

## Goals / Non-Goals

**Goals:** one declaration per parameter, compile-time typos, identity-only `ParameterDef`, configuration logic in Core, MQTT keys in Mqtt.

**Non-Goals:** turning parameters into an enum, changing parameter values or groups, changing Open-Meteo requests, changing any key or payload.

## Decisions

1. **Fields register into a catalog list.** `private static readonly List<ParameterDef> Catalog = [];` is declared first; each public field initializer calls `Hourly(...)`/`Daily(...)`, which create the def and add it to `Catalog`. `All`, `GetByGroup` and `GetByApiName` read the catalog. Static field initializers run in textual order, so the list is ready before the first parameter; a spec asserts the expected total count and that no two entries share API name and granularity.
2. **All parameters become fields**, including those without a typed property today (`showers`, `snow_depth`, soil moisture, ...). Daily variants of shared names get explicit names: `DailyWeatherCode` (exists), `DailySunshineDuration`, `DailyEt0FaoEvapotranspiration`. Existing property names stay as they are, so call sites do not change.
3. **`DeviceClass` is deleted, not moved.** Nothing outside the unit check reads it. The check validates by unit (`°C`, `m/s`, `hPa`), which covers exactly the parameters that carried `temperature`, `wind_speed` and `atmospheric_pressure` today; a spec pins this equivalence over the whole catalog.
4. **`ToMqttKey()` lives in `Njord.Mqtt`.** Enum switch-expressions do not apply to `ParameterDef` (a record, not an enum), so the keys are an explicit `FrozenDictionary<ParameterDef, string>` in `ParameterMqttKeys`, built from the same values as the old `JsonKey`s, with a spec asserting every catalog entry has a key (a new parameter without a key fails the spec, not production). Payload keys stay byte-identical.
5. **Group parsing is an exhaustive switch** over `ParameterGroup` via a `TryParseGroup` helper in `ParameterSelection` (names compared case-insensitively against `nameof` of each member); no `Enum.TryParse`. The allow-list entry for `ParameterRegistry` in the enum architecture spec is removed.
6. **`ResolvedParameterSet` stays in `Njord.Weather`** (used by Weather, OpenMeteo, Compute); `ParameterResolutionException` and `ParameterSelection` go to Core. `ParametersOptions.Resolve()` (from `consistent-mappings`) calls `ParameterSelection.Resolve`.

## Risks / Trade-offs

- Static field initialization order is a subtle dependency; the catalog-list-first rule plus the count/uniqueness spec contains it.
- 90 fields is longer than a table but each line is checked by the compiler and readable at the call site.
- The MQTT key table is a second place to touch for a new parameter; the completeness spec makes that explicit, and the key was already a separate piece of data.

## Migration Plan

Order: (1) selection to Core with unchanged messages, (2) registry fields and catalog, (3) remove `DeviceClass` and switch the unit check, (4) MQTT key table and remove `JsonKey`, (5) shrink allow-list. Each step keeps the golden masters, the client URI specs and the registry specs green.
