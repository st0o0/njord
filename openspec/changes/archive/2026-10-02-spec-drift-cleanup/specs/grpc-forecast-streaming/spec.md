## REMOVED Requirements

### Requirement: StreamForecasts pushes forecast updates in real-time
**Reason**: The v1 `njord.v1` protos and services (`ConfigService`, `ForecastService`) no longer exist; only `protos/njord/v2` is built.
**Migration**: `WeatherService.StreamForecasts` in `grpc-v2-weather-service` (multi-client and disconnect scenarios carried over).
