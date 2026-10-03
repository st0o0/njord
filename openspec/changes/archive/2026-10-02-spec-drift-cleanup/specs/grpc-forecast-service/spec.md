## REMOVED Requirements

### Requirement: ForecastService exposes location and model metadata
**Reason**: The v1 `njord.v1` protos and services (`ConfigService`, `ForecastService`) no longer exist; only `protos/njord/v2` is built.
**Migration**: `WeatherService.GetCatalog`, `GetForecast` and the streaming RPCs in `grpc-v2-weather-service` (unknown-model scenario carried over).

### Requirement: ForecastService returns forecast data for a model
**Reason**: The v1 `njord.v1` protos and services (`ConfigService`, `ForecastService`) no longer exist; only `protos/njord/v2` is built.
**Migration**: `WeatherService.GetCatalog`, `GetForecast` and the streaming RPCs in `grpc-v2-weather-service` (unknown-model scenario carried over).

### Requirement: Proto files define the service contract
**Reason**: The v1 `njord.v1` protos and services (`ConfigService`, `ForecastService`) no longer exist; only `protos/njord/v2` is built.
**Migration**: `WeatherService.GetCatalog`, `GetForecast` and the streaming RPCs in `grpc-v2-weather-service` (unknown-model scenario carried over).
