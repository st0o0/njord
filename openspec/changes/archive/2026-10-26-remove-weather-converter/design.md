## Context

`WeatherResponseConverter` is the only custom `JsonConverter` in the project. It exists because the
Open-Meteo ensemble endpoint returns dynamic keys: with multiple models requested, every hourly
variable becomes `<variable>_<modelid>` instead of plain `<variable>`. Model ids contain underscores
and digits, so a naive split is not possible; the converter walks a longest-prefix table of 32
known variable names.

The other five domain DTOs use plain attribute-based deserialization. Flood already handles dynamic
keys (`river_discharge_memberNN`) via `[JsonExtensionData]` and a mapping-layer filter.

After the `api-variable-enums` change, all 32 variable names exist as `[JsonStringEnumMemberName]`
on `WeatherApiVariable` and are cached in `ApiVariableNames`. The converter duplicates them.

## Goals / Non-Goals

**Goals:**
- Single source of truth for weather API variable names (`ApiVariableNames`)
- Weather DTO follows the same pattern as other domains (no custom converter)
- Multi-model split logic is testable independently of JSON deserialization
- All query-parameter keys and fixed values use named constants

**Non-Goals:**
- Changing `WeatherHourlyData` fields or `WeatherApiVariable` members
- Reworking multi-model behavior or the `FetchSuccess.Models` contract
- Touching other domain DTOs

## Decisions

### 1. Dictionary-based hourly block (like Flood)

`WeatherResponse.Hourly` becomes `Dictionary<string, JsonElement>?` with `[JsonPropertyName("hourly")]`.
The `[JsonConverter]` attribute is removed. `WeatherHourlyData` stays as a typed record but is built
in the mapping layer, not the deserialization layer.

**Alternative considered:** `[JsonExtensionData]` on a wrapper record. Rejected because the weather
response has only `latitude`, `longitude`, and `hourly` at the top level, so a direct
`Dictionary<string, JsonElement>` for the hourly property is simpler and avoids a wrapper.

### 2. Longest-prefix splitter in the mapping layer

`WeatherResponseMappingExtensions` gets a private helper that:
1. Gets known names from `ApiVariableNames.For<WeatherApiVariable>().Values`, sorted by length
   descending (longest prefix first, same as the current converter).
2. For each key in the hourly dictionary:
   - Skips `"time"`.
   - Matches against the sorted names. Exact match = plain variable. Prefix + `_` = suffixed.
   - If the suffix starts with `"member"`, skips (member-series filter).
   - Otherwise groups `(variableName, modelId?) -> JsonElement` (the raw array).
3. For each model group, deserializes each `JsonElement` array into `double?[]` and builds a
   `WeatherHourlyData` via a constructor or factory that takes the variable-to-array map.

The `time` array is extracted separately from the dictionary.

**Alternative considered:** Deserializing each array into `WeatherHourlyData` via
`JsonSerializer.Deserialize` on a reconstructed JSON object (like the current converter does).
Acceptable and simpler; avoids a variable-to-property mapping step. The trade-off is building a
`JsonObject` per model in the mapping layer instead of the converter. This is the preferred
approach because it keeps `WeatherHourlyData` deserialization via source-gen unchanged.

**Chosen approach:** Reconstruct a `JsonObject` per model (with `time` + the model's variable
arrays), then deserialize it as `WeatherHourlyData` using `OpenMeteoJsonContext.Default.WeatherHourlyData`.
This is exactly what the converter does today, just in the mapping layer. `WeatherHourlyData` stays
in `OpenMeteoJsonContext` for this reason.

### 3. Query-key constants on ClientRequest

`ClientRequest` already has constants for `Hourly`, `Daily`, `Monthly`, `Models`, `Latitude`,
`Longitude`, `Timeformat`, `Timezone`, `Unixtime`, `Utc`. Add:
- `ForecastDays`, `ForecastHours`, `PastHours` (query keys)
- `Tilt`, `Azimuth` (query keys)
- `WindSpeedUnit` (query key), `MetersPerSecond` (value)
- `Ensemble` (query key)
- `ToApiAzimuth(int configAzimuth)` static method (subtracts 180)

### 4. GlobalTiltedIrradiance const replaced

`WeatherClient.GlobalTiltedIrradiance` (a private const `"global_tilted_irradiance"`) is replaced
with `ApiVariableNames.For<WeatherApiVariable>()[WeatherApiVariable.GlobalTiltedIrradiance]`.
Or simpler: `WeatherApiVariable.GlobalTiltedIrradiance.ToApiName()`.

## Risks / Trade-offs

- [Perf] Building a `JsonObject` per model in the mapping layer adds one allocation per model per
  poll cycle. Negligible: the current converter does the same, and weather polls at most every
  60 minutes.
- [AOT] `WeatherHourlyData` must stay in `OpenMeteoJsonContext` because the mapping layer
  deserializes it via `JsonSerializer.Deserialize`. This is unchanged from today.
- [Breaking] The `WeatherResponseConverter.ApiNames` static field is used in
  `WeatherModelResponseSpec`. The test must be updated to use `ApiVariableNames` instead. No
  production code references it outside the converter.
