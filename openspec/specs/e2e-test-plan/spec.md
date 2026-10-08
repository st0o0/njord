## ADDED Requirements

### Requirement: Test plan document defines all E2E phases

The E2E test plan (`e2e/E2E-TEST-PLAN.md`) SHALL define 8 sequential phases covering the full gRPC path from njord through ha-njord to Home Assistant entities.

#### Scenario: Phase 0 — Stack setup
- **WHEN** the E2E test starts
- **THEN** the plan instructs: `docker compose down -v`, build njord image, `docker compose up -d`, poll njord `/alive` and HA `:8123` until both respond healthy

#### Scenario: Phase 1 — HA integration setup via browser
- **WHEN** both services are healthy
- **THEN** the plan instructs: open HA in browser (claude-in-chrome), navigate to Settings → Integrations → Add Integration, search "njord", enter host=`njord` and port=`8081`, submit the config flow

#### Scenario: Phase 2 — Entity registration
- **WHEN** the integration is configured
- **THEN** the plan instructs: poll HA REST API (`GET /api/states`) until weather entities appear, document the time from setup to first entities, record all entity IDs

#### Scenario: Phase 3 — Forecast data validation
- **WHEN** weather entities exist
- **THEN** the plan instructs: verify `weather.<loc>_<model>` entities have non-null temperature, verify `native_wind_speed_unit` is `m/s` (HA may convert the display `wind_speed_unit` to the user's unit system), verify hourly forecast service returns entries with temperature/humidity/wind_speed, verify consensus entity has agreement and spread attributes

#### Scenario: Phase 4 — Enrichment entity validation
- **WHEN** enrichment entities exist
- **THEN** the plan instructs: verify 14 alert sensors, 11 index sensors, trend sensor, 5 derived sensors, and history sensor exist with correct device_class and attributes (severity, confidence for alerts; numeric values for indices)

#### Scenario: Phase 5 — Connectivity and server entities
- **WHEN** the system is running
- **THEN** the plan instructs: verify 3 gRPC stream binary_sensors are `on`, verify server sensors (version, uptime, monthly_usage, daily_usage) have non-empty numeric values with correct units (version: no unit, uptime: no unit, monthly_usage: `%`, daily_usage: `%`), verify `button.trigger_poll` exists

#### Scenario: Phase 6 — Direct gRPC validation
- **WHEN** njord is running
- **THEN** the plan instructs: call `OpsService.GetStatus` and verify response contains version, locations, and models matching the Docker Compose config

#### Scenario: Phase 7 — Resilience
- **WHEN** njord is restarted (`docker restart njord`)
- **THEN** the plan instructs: poll njord `/alive` until healthy, wait up to 60 s and then poll HA until `binary_sensor.forecast_stream` returns to `on`, verify weather entities remain non-unavailable. The disconnect detection timeout exceeds 10 s due to gRPC keepalive and `expire_after` settings.

#### Scenario: Phase 8 — Trigger poll via browser
- **WHEN** `button.trigger_poll` is pressed in the HA UI
- **THEN** the plan instructs: verify enrichment entity timestamps (`last_updated` on a sensor such as `sensor.lucerne_weather_trend`) advance after the triggered poll. Weather entity `last_updated` may not advance if the Open-Meteo API returns identical forecast data.

### Requirement: Entity expectations are derived from config

The test plan SHALL derive expected entity IDs from the Docker Compose configuration (locations × models × enrichment features) rather than hardcoding them.

#### Scenario: Config-driven entity count
- **WHEN** the Docker Compose config has 1 location, 2 models, and all 6 enrichments enabled
- **THEN** the test plan expects approximately 47 entities: 3 weather, 38 sensors, 4 binary_sensors, 1 event, 1 button

#### Scenario: Entity ID pattern
- **WHEN** the location is "lucerne" and a model is "icon_d2"
- **THEN** expected entity IDs follow HA naming: `weather.lucerne_icon_d2`, `sensor.lucerne_frost_alert`, etc.

### Requirement: Results document format

Each E2E test run SHALL produce `e2e/results/E2E-TEST-RESULTS-<YYYY-MM-DD>.md` with a summary table.

#### Scenario: Result table structure
- **WHEN** the test run completes
- **THEN** the results document contains a table with columns: Phase, Step, Result (PASS/FAIL), Detail

#### Scenario: No SKIP allowed
- **WHEN** a test step cannot be executed
- **THEN** it SHALL be marked FAIL with explanation, never SKIP

### Requirement: Orchestration model

The test plan SHALL define an orchestration model using Opus as main agent with parallel Haiku subagents.

#### Scenario: Sequential phases
- **WHEN** the test begins
- **THEN** Opus executes Phase 0, 1, 2 sequentially (each depends on the previous)

#### Scenario: Parallel validation
- **WHEN** Phase 2 completes (entities registered)
- **THEN** Haiku subagent #1 executes Phase 3+4, Haiku subagent #2 executes Phase 5+6, in parallel

#### Scenario: Haiku FAIL verification
- **WHEN** a Haiku subagent reports a FAIL
- **THEN** the Opus main agent re-checks the failed step before accepting it as FAIL in the final results
