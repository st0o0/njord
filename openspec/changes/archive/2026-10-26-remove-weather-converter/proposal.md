## Why

`WeatherResponseConverter` is a 112-LOC custom `JsonConverter` that duplicates 32 API variable names
already present as `[JsonStringEnumMemberName]` attributes on `WeatherApiVariable`. It exists only
because the Open-Meteo ensemble endpoint returns dynamic keys (`temperature_2m_<modelid>`) that
cannot be deserialized with attribute-based binding. The converter makes `WeatherResponse` the only
domain DTO that needs special handling, and the duplicated literals are a maintenance hazard.

## What Changes

- **Remove `WeatherResponseConverter`** and its `ApiNames`/`BaseNames` frozen sets.
- **Simplify `WeatherResponse`** to a plain record with `Dictionary<string, JsonElement>` for the
  hourly block (the Flood precedent: `FloodDailyData` already uses `JsonExtensionData` for dynamic keys).
- **Move the multi-model split logic** into `WeatherResponseMappingExtensions`, deriving known
  variable names from `ApiVariableNames.For<WeatherApiVariable>()` instead of hard-coded literals.
- **Add query-key constants** to `ClientRequest` (`ForecastDays`, `PastHours`, `Tilt`, `Azimuth`,
  `WindSpeedUnit`, `Ensemble` and their values) and use them in all six clients.
- **Deduplicate the azimuth conversion** (`Azimuth - 180`) shared between `WeatherClient` and
  `SatelliteRadiationClient` into a helper on `ClientRequest`.
- **Replace `WeatherClient.GlobalTiltedIrradiance` const** with `ApiVariableNames` lookup.

## Non-goals

- Changing the multi-model feature behavior or the `FetchSuccess.Models` contract.
- Touching `WeatherHourlyData` fields or `WeatherApiVariable` members.
- Reworking other domain DTOs (they already follow the target pattern).
- Changing polling, budget or API call weight.

## Capabilities

### New Capabilities

_None._

### Modified Capabilities

- `domain-clients`: `WeatherClient` drops the converter dependency; all clients use shared
  query-key constants and the deduplicated azimuth helper.
- `api-variable-enums`: `ApiVariableNames` becomes the single source of truth for variable-name
  lookups in the mapping layer (not just the request-building layer).

## Impact

- `Njord.OpenMeteo`: `WeatherResponseConverter.cs` deleted; `WeatherResponse.cs`,
  `WeatherResponseMappingExtensions.cs`, `WeatherClient.cs`, `SatelliteRadiationClient.cs`,
  `ClientRequest.cs` modified; all other clients gain constant references.
- `Njord.OpenMeteo.Tests`: `WeatherModelResponseSpec` updated (no more `WeatherResponseConverter.ApiNames`),
  mapping tests adjusted for the dictionary-based response.
- `Http/OpenMeteoJsonContext.cs`: `WeatherHourlyData` registration may be removable if not needed
  by source-gen after the converter is gone.
- No API-budget impact (no change to polling or call weight).
