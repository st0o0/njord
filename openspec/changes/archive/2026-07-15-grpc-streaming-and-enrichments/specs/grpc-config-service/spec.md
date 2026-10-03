## ADDED Requirements

### Requirement: GetConfig returns current njord configuration
`ConfigService.GetConfig` SHALL be a unary RPC returning the current `NjordConfig` including locations (name, lat, lon, models), default models, horizons, forecast days, poll interval, parameter groups, and enrichment enabled flags.

#### Scenario: Config reflects current state
- **WHEN** a client calls `GetConfig`
- **THEN** the response SHALL contain all configured locations with their resolved models, the default model list, horizons [3,6,12,24,48,72], forecast_days 4, and enabled enrichment features

#### Scenario: Per-location models are resolved
- **WHEN** a location has per-location model overrides
- **THEN** the `LocationConfig.models` field SHALL contain the resolved model list for that location (merged with globals, deduplicated)

### Requirement: StreamConfig pushes config changes
`ConfigService.StreamConfig` SHALL be a server-streaming RPC. It SHALL send a full `NjordConfig` snapshot immediately on subscription (current state) and push a new snapshot whenever the configuration changes.

#### Scenario: Initial config sent on subscribe
- **WHEN** a client calls `StreamConfig`
- **THEN** it SHALL immediately receive one `NjordConfig` message with the current configuration

#### Scenario: Config change triggers push
- **WHEN** the njord configuration changes (e.g. via hot-reload or future config API)
- **THEN** all `StreamConfig` subscribers SHALL receive a new `NjordConfig` snapshot

#### Scenario: Stream ends on client disconnect
- **WHEN** a client disconnects from the `StreamConfig` stream
- **THEN** the server-side stream SHALL be disposed cleanly

### Requirement: ConfigService is a separate gRPC service
`ConfigService` SHALL be defined in `protos/njord/v1/config_service.proto` as a separate service from `ForecastService`. Clients that only need config information SHALL NOT need to import forecast message types.

#### Scenario: Proto compiles independently
- **WHEN** `config_service.proto` is compiled
- **THEN** it SHALL not depend on `forecast_service.proto`
