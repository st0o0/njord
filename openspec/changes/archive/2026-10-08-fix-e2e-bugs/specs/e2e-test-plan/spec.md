## MODIFIED Requirements

### Requirement: Test plan document defines all E2E phases

The E2E test plan (`e2e/E2E-TEST-PLAN.md`) SHALL define 8 sequential phases covering the full gRPC path from njord through ha-njord to Home Assistant entities.

#### Scenario: Phase 5 — Connectivity and server entities
- **WHEN** the system is running
- **THEN** the plan instructs: verify 3 gRPC stream binary_sensors are `on`, verify server sensors (version, uptime, monthly_usage, daily_usage) have non-empty numeric values with correct units (version: no unit, uptime: no unit, monthly_usage: `%`, daily_usage: `%`), verify `button.trigger_poll` exists

#### Scenario: Phase 3 — Forecast data validation
- **WHEN** weather entities exist
- **THEN** the plan instructs: verify `weather.<loc>_<model>` entities have non-null temperature, verify `native_wind_speed_unit` is `m/s` (HA may convert the display `wind_speed_unit` to the user's unit system), verify hourly forecast service returns entries with temperature/humidity/wind_speed, verify consensus entity has agreement and spread attributes

#### Scenario: Phase 8 — Trigger poll via browser
- **WHEN** `button.trigger_poll` is pressed in the HA UI
- **THEN** the plan instructs: verify enrichment entity timestamps (`last_updated` on a sensor such as `sensor.lucerne_weather_trend`) advance after the triggered poll. Weather entity `last_updated` may not advance if the Open-Meteo API returns identical forecast data.

#### Scenario: Phase 7 — Resilience
- **WHEN** njord is restarted (`docker restart njord`)
- **THEN** the plan instructs: poll njord `/alive` until healthy, wait up to 60 s and then poll HA until `binary_sensor.forecast_stream` returns to `on`, verify weather entities remain non-unavailable. The disconnect detection timeout exceeds 10 s due to gRPC keepalive and `expire_after` settings.
