## Why

Every field of a domain (Weather, AirQuality, Flood, Marine, Seasonal) lives in seven parallel lists that are kept in sync by hand: the `[Flags]` enum member, the DTO property with `[JsonPropertyName]`, the Point property, the `Has()` switch arm, the `ToApiName()` switch arm, the `AnyAt()` argument and the Point constructor argument in the response mapper. Weather has 32 fields, so 224 manual connections. A missed arm is a silent bug (field never requested, never mapped, or ignored by `HasFields`). One typed table per domain makes the compiler and a completeness spec guard the connections instead of reviewers.

## What Changes

- Add `FieldDefinition<TParams, TDto, TPoint>` and `FieldConversion` (None, IntCast, BoolThreshold) as the shared vocabulary for a field table.
- Add one static table per domain (`WeatherFieldMap`, `AirQualityFieldMap`, `MarineFieldMap`, `FloodFieldMap`, `SeasonalFieldMap`) as the single source of truth: flag, API name, typed DTO accessor, typed Point setter, conversion.
- Derive from the table: `ToApiNames()`, `HasFields()`, the "any value at index" check and the generic `ToDomainForecast` loop (null guard, lastValued tracking, per-field read with conversion, trailing trim, `ImmutableArray` construction).
- Delete the hand-written `Has()` switches, `ToApiName()` switches, `AnyAt(...)` argument lists and the 32-argument constructor calls in the Weather, AirQuality and Marine mappers; Seasonal follows if parity holds (see design D6).
- Flood: `ToApiNames` and `HasFields` come from the table; the ensemble-member parsing from `JsonExtensionData` stays hand-written.
- `OpenMeteoRequestBuilder` URL-encodes keys and values instead of concatenating raw strings.
- A completeness spec per domain fails when an enum member has no table entry, an entry is duplicated, or a Point property has no entry.
- No source generator, no new project, no reflection at runtime. Observable behavior of mappers, `HasFields` and URLs is unchanged; existing tests pass unchanged.

## Capabilities

### New Capabilities

- `typed-field-mappings`: per-domain field tables as the single source of truth for API names, presence checks and response-to-point mapping, including completeness guarantees.
- `request-url-encoding`: Open-Meteo request URLs encode keys and values; comma-separated variable lists stay readable.

### Modified Capabilities

None. `param-enums` ("Points report field presence") and the mapping behavior stay as specified; only their implementation changes.

## Non-goals

- A Roslyn/source generator or any new analyzer project.
- Generating the DTO records (System.Text.Json source generation keeps handling serialization) or the Params enums (hand-maintained).
- Generating gRPC/proto mappings or touching `ToProto()` mappers.
- Runtime reflection, expression trees or AutoMapper.
- Changing Flood ensemble-member parsing.
- Adding, removing or renaming any requested variable.

## API-budget impact

None. The set of requested variables, call weights (`ceil(variables / 10)` for Weather, 1 otherwise), poll cadence and URLs (percent-encoding of already-safe characters is a no-op, so URLs are identical) are unchanged. `BudgetCalculator` is untouched.

## Impact

- `src/Njord.Forecast/` (`HasFields` implementations on the five Point types), `src/Njord.OpenMeteo/Mapping/` (tables, generic mapper, deleted switches and mappers), `src/Njord.OpenMeteo/OpenMeteoRequestBuilder.cs`.
- `src/Njord.Forecast.Tests`, `src/Njord.OpenMeteo.Tests`: new completeness and parity specs; existing specs unchanged.
- `Njord.Architecture.Tests`: Forecast must stay free of Njord references; the table vocabulary lives where that holds (design D2).
- No public wire, proto, config or persistence changes; no docs/Config Builder changes.
