# admin-api Specification

## Purpose

ASP.NET minimal API endpoints that expose configuration CRUD, stats time-series, and health/status queries for the admin SPA.

## ADDED Requirements

### Requirement: Config API exposes current configuration
The system SHALL provide `GET /api/config` that returns the current `NjordOptions` as JSON. The response SHALL include all sections (locations, models, horizons, mqtt, enrichment, persistence, parameters, poll interval, budget override). Sensitive fields (MQTT password) SHALL be masked in the response.

#### Scenario: Full config returned
- **WHEN** `GET /api/config` is called
- **THEN** the response is 200 with the complete `NjordOptions` serialized as JSON

#### Scenario: MQTT password is masked
- **WHEN** `GET /api/config` is called and an MQTT password is configured
- **THEN** the `mqtt.password` field is `"***"` (not the actual password)

### Requirement: Config API accepts section updates
The system SHALL provide `PUT /api/config/{section}` where `{section}` is one of: `locations`, `models`, `horizons`, `pollInterval`, `forecastDays`, `discoveryInterval`, `budgetOverride`, `parameters`, `enrichment`. The request body SHALL contain the new value for that section. The endpoint SHALL validate the resulting configuration before applying it.

#### Scenario: Update poll interval
- **WHEN** `PUT /api/config/pollInterval` is called with body `"00:30:00"`
- **THEN** the writable provider is updated and `IOptionsMonitor` fires

#### Scenario: Update locations
- **WHEN** `PUT /api/config/locations` is called with a JSON array of location objects
- **THEN** the writable provider sets all `Njord:Locations:N:*` keys accordingly

#### Scenario: Validation failure returns 400
- **WHEN** `PUT /api/config/horizons` is called with `[0, -1]`
- **THEN** the response is 400 with validation error details

#### Scenario: Successful update returns updated config
- **WHEN** a valid `PUT /api/config/{section}` completes
- **THEN** the response is 200 with the full updated `NjordOptions`

### Requirement: Config API distinguishes hot-reloadable from restart-required settings
The system SHALL provide `GET /api/config/metadata` that returns, per config key, whether it is hot-reloadable or requires a container restart. The metadata SHALL classify MQTT connection settings and persistence provider as restart-required.

#### Scenario: Metadata returned
- **WHEN** `GET /api/config/metadata` is called
- **THEN** the response includes `{"mqtt.host": {"hotReload": false}, "enrichment.alerts.frostThreshold": {"hotReload": true}, ...}`

### Requirement: Config API exposes override state
The system SHALL provide `GET /api/config/overrides` that returns only the keys currently overridden by the writable provider (not the full merged config). This allows the UI to show which values differ from defaults.

#### Scenario: No overrides
- **WHEN** `GET /api/config/overrides` is called and no overrides exist
- **THEN** the response is 200 with an empty object `{}`

#### Scenario: Active overrides listed
- **WHEN** the writable provider has overrides for `Njord:PollInterval` and `Njord:Enrichment:Alerts:FrostThreshold`
- **THEN** the response lists those keys with their override values

### Requirement: Config API supports resetting overrides
The system SHALL provide `DELETE /api/config/overrides/{key}` that removes a specific override from the writable provider, reverting to the base value. `DELETE /api/config/overrides` (no key) SHALL remove all overrides.

#### Scenario: Reset single override
- **WHEN** `DELETE /api/config/overrides/Njord:PollInterval` is called
- **THEN** the poll interval reverts to the value from appsettings.json or env vars

#### Scenario: Reset all overrides
- **WHEN** `DELETE /api/config/overrides` is called
- **THEN** all overrides are removed and the full config reverts to base values

### Requirement: Stats API exposes time-series data
The system SHALL provide `GET /api/stats/timeseries?metric={name}&window={duration}` that returns time-bucketed values from the in-process stats collector. Supported metrics SHALL include: `fetch.total`, `fetch.failures`, `fetch.duration`, `mqtt.publishes`, `mqtt.connected`, `mqtt.reconnects`, `polls.total`, `data.changes`.

#### Scenario: Fetch total time-series
- **WHEN** `GET /api/stats/timeseries?metric=fetch.total&window=1h` is called
- **THEN** the response contains an array of `{timestamp, value}` points covering the last hour

#### Scenario: Unknown metric returns 404
- **WHEN** `GET /api/stats/timeseries?metric=nonexistent` is called
- **THEN** the response is 404

### Requirement: Stats API exposes current counters
The system SHALL provide `GET /api/stats/current` that returns a snapshot of all current counter values (totals since startup), the last successful poll time, MQTT connection state, and service uptime.

#### Scenario: Current stats snapshot
- **WHEN** `GET /api/stats/current` is called
- **THEN** the response includes `fetchTotal`, `fetchFailures`, `mqttPublishes`, `mqttConnected`, `uptime`, `lastPollUtc`

### Requirement: Stats API exposes budget projection
The system SHALL provide `GET /api/stats/budget` that returns the current API budget status: projected monthly usage, actual usage since month start, resolved monthly limit, and percentage used.

#### Scenario: Budget status returned
- **WHEN** `GET /api/stats/budget` is called
- **THEN** the response includes `projected`, `actual`, `limit`, `percentUsed`

### Requirement: Health API extends existing health endpoints
The system SHALL provide `GET /api/health` that returns structured health information beyond the existing `/healthz` endpoint: per-component health (MQTT connection, pipeline, each enrichment feature), scheduler state per model (phase, miss count, next poll), and discovery state.

#### Scenario: Structured health returned
- **WHEN** `GET /api/health` is called
- **THEN** the response includes component-level health with status, details, and timestamps

### Requirement: All API endpoints are under /api prefix
All admin API endpoints SHALL be mapped under the `/api` prefix. The existing `/healthz` and `/alive` endpoints SHALL remain unchanged. API endpoints SHALL return JSON with `Content-Type: application/json`.

#### Scenario: API prefix routing
- **WHEN** a request is made to `/api/config`
- **THEN** it is routed to the config API, not to the SPA fallback

#### Scenario: SPA fallback for non-API paths
- **WHEN** a request is made to `/settings` (a Vue route)
- **THEN** it is routed to the SPA's `index.html` fallback
