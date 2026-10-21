## Purpose

Defines the E2E test plan document: its sequential phases, config-derived entity expectations, results document format and the orchestration model of main agent and subagents.
## Requirements
### Requirement: Entity expectations are derived from config

The test plan SHALL derive expected entity IDs from the Docker Compose configuration (locations × models × enrichment features) rather than hardcoding them.

#### Scenario: Config-driven entity count
- **WHEN** the Docker Compose config has 1 location, 2 models, and all 5 analysis features enabled
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



### Requirement: Plan expectations match real service behaviour
`e2e/E2E-TEST-PLAN.md` SHALL describe the observable behaviour of the current service and Home Assistant integration, so a correct run produces no false failures.

#### Scenario: Forecast and alert checks
- **WHEN** a tester follows S4.4, S7.3 and S7.5
- **THEN** S4.4 calls the `weather.get_forecasts` service (type hourly, `?return_response`) and expects a non-empty list (daily is unsupported); S7.3 expects `severity` only on alerts in state on, with a value in {yellow, orange, red}; S7.5 uses `pm2_5_alert`, not `pm25_alert`; the alert count is 21 entities matching `*_alert`

#### Scenario: gRPC response shapes
- **WHEN** a tester follows S8 and S9
- **THEN** S8.1/S8.2 expect the oneof JSON names `weather` / `airQuality`; S8.5 allows `precipitation` to be omitted when 0; Marine returns NotFound for inland locations by design; S9 expects `events[]` where the feature is the oneof case (e.g. `fog`), alert severity at `<case>.alert.severity`, no `type_name`, and S9.4 checks that an event has a feature oneof case

#### Scenario: Ops, health and budget checks
- **WHEN** a tester follows S10, S11, S12 and S13.4
- **THEN** S10.1 uses `GetCatalog` on `njord.v2.WeatherService`; S10.3 expects poll state per location (target phase and cycle); S11.1 treats `server_monthly_usage` as a percentage; S12.1 expects `/healthz` plain text `Healthy` or `Degraded`; S13.4 expects the increment to equal the sum of call weights of the resolved domains (e.g. 11 for the e2e config)
