## Why

Weather forecast icons in Home Assistant are missing or wrong because njord's condition strings (`mostly-clear`, `partly-cloudy`, `freezing-drizzle`, etc.) don't match what ha-njord's `_CONDITION_MAP` expects (underscored keys, missing entries). The root cause is a fragile double-translation: njord converts WMO integer codes to custom strings, then ha-njord converts those strings to HA condition constants. Information is also lost: `is_day` (needed for sunny vs clear-night distinction) is never sent, so nighttime clear-sky icons are wrong.

## What Changes

- **BREAKING**: `WeatherValue` and `WeatherForecastPoint` proto messages gain `weather_code` (int32) and `is_day` (bool) fields; the existing `condition` string field stays but becomes a human-readable label (not used for icon mapping)
- WeatherRule adds `ForecastParams.IsDay` to its requirements and passes raw WMO code + is_day through to the result
- `WeatherResult` and `WeatherForecastPoint` (Messages) gain `WeatherCode` (int) and `IsDay` (bool) fields
- gRPC WeatherValue mapper sends the new fields
- ha-njord `weather.py` switches from `_CONDITION_MAP` string lookup to `condition_mapper.map_condition(weather_code, is_day)` which already exists and handles day/night correctly
- ha-njord `_CONDITION_MAP` is removed; `condition_mapper.py` becomes the single source of truth for WMO-to-HA condition mapping

## Non-goals

- Changing WMO code interpretation logic (the existing `condition_mapper.py` mapping is correct)
- Adding daily forecast support (only hourly exists today)
- Changing any non-weather feature's condition/icon handling

## Capabilities

### New Capabilities

_None_

### Modified Capabilities

- `weather-feature`: WeatherResult and WeatherForecastPoint gain weather_code + is_day fields; WeatherRule requires IsDay from Forecast domain
- `v3-proto`: WeatherValue and WeatherForecastPoint proto messages gain weather_code and is_day fields

## Impact

- **Proto**: Two new optional fields each on `WeatherValue` and `WeatherForecastPoint` (wire-compatible addition)
- **njord**: `Njord.Features/Weather/WeatherRule.cs`, `Njord.Messages` (WeatherResult, WeatherForecastPoint records), `Njord.Grpc/Mapping/WeatherValueMappingExtensions.cs`
- **ha-njord**: `weather.py` (icon mapping), `condition_mapper.py` (now used)
- **API budget**: IsDay is a free parameter on the Forecast API (no extra call weight), zero budget impact
