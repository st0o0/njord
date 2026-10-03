## MODIFIED Requirements

### Requirement: ForecastService returns forecast data for a model
The `ForecastService.GetForecast` RPC SHALL query the `ForecastSnapshotActor` via Ask to retrieve the latest `ModelForecast`. It SHALL map the `ModelForecast` directly to the proto `GetForecastResponse` without intermediate DTOs. If the actor returns null, the RPC SHALL return gRPC status `NOT_FOUND`.

#### Scenario: Successful forecast query via actor Ask
- **WHEN** a client calls `GetForecast` with location "lucerne" and model "icon_d2"
- **THEN** the service SHALL Ask `ForecastSnapshotActor` for the forecast and map the `ModelForecast` to the proto response

#### Scenario: Actor timeout returns UNAVAILABLE
- **WHEN** the `ForecastSnapshotActor` does not respond within the timeout
- **THEN** the RPC SHALL return gRPC status `UNAVAILABLE`
