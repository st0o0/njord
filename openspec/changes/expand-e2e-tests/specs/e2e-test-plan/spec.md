## MODIFIED Requirements

### Requirement: Test plan document defines all E2E phases

The E2E test plan (`e2e/E2E-TEST-PLAN.md`) SHALL define ~25 sections covering the full gRPC path from njord through ha-njord to Home Assistant entities, plus exhaustive gRPC API validation, entity attribute depth, enrichment computation verification, error handling, multi-poll-cycle testing, configuration scenarios, HA browser verification, and teardown.

#### Scenario: Phase 0 — Stack setup
- **WHEN** the E2E test starts
- **THEN** the plan instructs: `docker compose down -v`, build njord image, `docker compose up -d`, poll njord `/alive` and HA `:8123` until both respond healthy

#### Scenario: Phase 1 — HA integration setup via browser
- **WHEN** both services are healthy
- **THEN** the plan instructs: open HA in browser (claude-in-chrome), complete onboarding, create long-lived access token, navigate to Settings → Integrations → Add Integration, search "njord", enter host=`njord` and port=`8081`, submit the config flow

#### Scenario: Phase 2 — Entity registration
- **WHEN** the integration is configured
- **THEN** the plan instructs: poll HA REST API (`GET /api/states`) until weather entities appear, verify ~47 entities exist, document the time from setup to first entities, record all entity IDs

#### Scenario: Phase 3 — Forecast data validation
- **WHEN** weather entities exist
- **THEN** the plan instructs: verify weather entity states are valid conditions, verify attributes include temperature/humidity/pressure/wind_speed/wind_bearing with correct units (°C, m/s), verify hourly forecast service returns entries, verify consensus entity has agreement/spread/models_used attributes

#### Scenario: Phase 4 — Enrichment entity validation with depth
- **WHEN** enrichment entities exist
- **THEN** the plan instructs: verify 14 alert sensors with severity (none/low/medium/high/extreme) and confidence (0–100), verify 11 index sensors with values in range (0–10 for standard, documented ranges for VPD/frost), verify trend sensor has descriptive state, verify 5 derived sensors with correct values and units, verify history sensor has state

#### Scenario: Phase 5 — Connectivity and server entities
- **WHEN** the system is running
- **THEN** the plan instructs: verify 3 gRPC stream binary_sensors are `on`, verify server sensors have correct values and units (version matches semver, usage in requests, uptime as duration), verify target sensors exist

#### Scenario: Phases 6–9 — Exhaustive gRPC API validation
- **WHEN** njord is running
- **THEN** the plan instructs: test all 16 RPCs across WeatherService (GetCatalog, GetForecast, GetEnrichments, StreamForecasts, StreamEnrichments), OpsService (GetStatus, GetTargets, TriggerPoll), AdminService (GetConfig, StreamConfig, SetLocations, SetSettings, SetEnrichment, SetBudget), SensorService (Push, StreamPush)

#### Scenario: Phases 10–11 — Error handling
- **WHEN** error scenarios are tested
- **THEN** the plan instructs: send invalid gRPC requests (unknown location, invalid model, bad sensor kind, empty location list) and verify appropriate gRPC error codes; verify HA REST API error responses for non-existent entities and invalid service calls

#### Scenario: Phases 12–13 — Multi-poll-cycle validation
- **WHEN** multiple poll cycles are triggered
- **THEN** the plan instructs: trigger a second poll cycle, verify timestamps update, verify usage counters increment, verify budget tracking consistency between gRPC and HA entities

#### Scenario: Phases 14–16 — Configuration scenarios
- **WHEN** config mutations are applied
- **THEN** the plan instructs: disable/re-enable enrichment features via AdminService, verify entity set adjusts; modify settings via AdminService, verify GetConfig and StreamConfig reflect changes

#### Scenario: Phase 17 — HA browser verification
- **WHEN** browser verification is needed
- **THEN** the plan instructs: verify weather cards display temperature/humidity/wind, verify entity attributes in Developer Tools, verify entity filter works, trigger poll via button in HA UI

#### Scenario: Phase 18 — Resilience
- **WHEN** njord is restarted (`docker restart njord`)
- **THEN** the plan instructs: poll njord `/alive` until healthy, poll HA until streams reconnect, verify entities return to non-unavailable states, verify entity states during downtime show unavailable

#### Scenario: Phase 19 — Teardown
- **WHEN** teardown is tested
- **THEN** the plan instructs: remove njord integration via HA, verify all entities are cleaned up, run `docker compose down -v`, verify clean shutdown

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

The test plan SHALL define an orchestration model using Opus as main agent with three parallel Haiku subagents.

#### Scenario: Sequential phases
- **WHEN** the test begins
- **THEN** Opus executes Phase 0, 1, 2 sequentially (each depends on the previous)

#### Scenario: Parallel validation with three subagents
- **WHEN** Phase 2 completes (entities registered)
- **THEN** three Haiku subagents are spawned in a single message: #1 for forecast/enrichment validation, #2 for connectivity/server/gRPC API, #3 for error handling

#### Scenario: Sequential post-parallel phases
- **WHEN** all parallel subagents complete
- **THEN** Opus executes multi-cycle, config scenarios, HA browser, resilience, and teardown phases sequentially

#### Scenario: Haiku FAIL verification
- **WHEN** a Haiku subagent reports a FAIL
- **THEN** the Opus main agent re-checks the failed step before accepting it as FAIL in the final results

### Requirement: Setup recipes

The test plan SHALL define named setup recipes for deterministic test scenarios.

#### Scenario: Recipe documentation
- **WHEN** a test section requires a specific configuration state
- **THEN** the test plan documents the setup recipe (AdminService calls or Docker config overrides) needed to reach that state

#### Scenario: State reset between scenarios
- **WHEN** a config scenario test modifies the running config
- **THEN** the test plan documents how to restore the default config before proceeding to the next scenario
