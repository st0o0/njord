## Why

njord's gRPC API currently includes HA-specific `condition` fields (e.g. `"sunny"`, `"rainy"`) derived from WMO weather codes via `WeatherConditionMapper`. This couples njord to Home Assistant's condition vocabulary — knowledge that belongs in the consumer, not the data provider. The HA integration (future HACS project) should own the WMO-to-HA mapping so it can iterate independently and other consumers aren't forced into HA semantics.

## What Changes

- **Remove** `WeatherConditionMapper` class and its tests from njord.
- **Remove** `condition` string fields from `HourlyForecast`, `DailyForecast`, and `GetForecastResponse` in the proto definition. The raw `weather_code` and `is_day` fields remain — consumers map these to whatever condition vocabulary they need.
- **Remove** condition mapping logic from `ForecastGrpcService.MapResponse()`.
- **Update** gRPC service tests to drop condition assertions.

## Non-goals

- Adding any new fields to the proto — this is pure removal.
- Building the HA integration's condition mapper — that's the HA integration project's scope.

## Capabilities

### New Capabilities

_(none)_

### Modified Capabilities

- `grpc-forecast-service`: Remove `condition` fields from proto messages and gRPC responses. The `weather_code` and `is_day` fields remain as the raw data source.

## REMOVED Capabilities

- `weather-condition-mapping`: The `WeatherConditionMapper` and its spec are removed entirely. The mapping responsibility moves to consumers.

## Impact

- **Proto**: 3 fields removed (`condition` in `GetForecastResponse`, `HourlyForecast`, `DailyForecast`). Backward-compatible since proto3 ignores unknown fields — but no existing external consumers exist yet.
- **Code**: `WeatherConditionMapper.cs` deleted, `ForecastGrpcService.cs` simplified, test assertions updated.
- **API budget**: Zero impact — no polling changes.
