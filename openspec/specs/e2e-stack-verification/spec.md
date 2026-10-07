# e2e-stack-verification Specification

## Purpose

Verifies the full njord + ha-njord + Home Assistant stack actually works together end-to-end — something unit and actor tests can't reach — via an agent-orchestrated run against real Docker containers and a real Home Assistant instance, rather than a simulated MQTT subscriber.

## Requirements

### Requirement: The e2e-test skill runs the real stack via Docker Compose
The `e2e-test` Claude Code skill SHALL start njord, ha-njord, Home Assistant, and a Mosquitto broker via `e2e/docker-compose.e2e.yml`, and poll both njord's `/alive` and Home Assistant's root endpoint until healthy before proceeding.

#### Scenario: Stack comes up healthy
- **WHEN** the skill runs Phase 0 (Stack Setup)
- **THEN** `docker compose -f e2e/docker-compose.e2e.yml up -d --build` SHALL be followed by njord `/alive` returning 200 within 60s and Home Assistant returning 200/302 within 120s

### Requirement: Home Assistant is driven through its real UI and REST API
The skill SHALL complete Home Assistant onboarding (or skip if already onboarded), create a long-lived access token via the UI, and add the njord integration via the UI — then verify everything else (entities, forecast data, enrichment, connectivity, gRPC, resilience, poll trigger) through Home Assistant's REST API, not a simulated MQTT subscriber.

#### Scenario: Integration setup succeeds
- **WHEN** the njord integration is added with host `njord` and port `8081`
- **THEN** Home Assistant SHALL report success with location/model counts, and weather entities SHALL appear in `/api/states` within 120s

### Requirement: Verification runs in parallel subagents with a FAIL re-check pass
The skill SHALL spawn two subagents in parallel to verify different entity groups against the HA REST API, then re-run any reported FAIL itself before accepting it, to distinguish a subagent misinterpretation from a real bug.

#### Scenario: Parallel verification covers forecast, enrichment, connectivity, and gRPC
- **WHEN** entities have registered
- **THEN** one subagent verifies forecast + enrichment entities and another verifies connectivity/server-diagnostic entities + direct gRPC calls, both reporting PASS/FAIL per step

#### Scenario: Reported FAIL is independently re-checked
- **WHEN** a subagent reports a FAIL
- **THEN** the orchestrating run SHALL re-run that specific check itself before the result is written as FAIL

### Requirement: Resilience and manual poll trigger are verified against the live stack
The skill SHALL restart the njord container and verify reconnection and entity-availability recovery, then trigger a manual poll (via the HA button entity or REST service call) and verify forecast data refreshes.

#### Scenario: Container restart recovers
- **WHEN** `docker restart njord-e2e` runs
- **THEN** `/alive` SHALL return 200 within 60s, `binary_sensor.forecast_stream` SHALL become `on` within 120s, and `weather.lucerne_icon_d2` SHALL not be `unavailable`

#### Scenario: Manual poll trigger refreshes data
- **WHEN** `button.trigger_poll` is pressed via the REST API
- **THEN** `weather.lucerne_icon_d2`'s `last_updated` SHALL advance within 30s

### Requirement: Every step gets a PASS/FAIL result, written to a dated results file
The skill SHALL record every step's result (never SKIP) with PASS/FAIL, timing baselines, the full observed entity list, and any notes, in `e2e/results/E2E-TEST-RESULTS-<YYYY-MM-DD>.md`, then tear the stack down.

#### Scenario: Results file is written and stack is torn down
- **WHEN** all phases complete
- **THEN** a results file SHALL exist with a summary table, timing baselines, and entity list, and `docker compose -f e2e/docker-compose.e2e.yml down -v` SHALL have run

### Requirement: MQTT payload regression detection is an acknowledged gap
This skill SHALL NOT provide byte-exact MQTT discovery/state payload regression detection via Verify snapshots, SHALL NOT separately verify multi-model consensus payload detail, and SHALL NOT verify HA birth/re-discovery behavior on `homeassistant/status`. A future, narrower proposal would be needed to close this gap; it is not blocking.

#### Scenario: No MQTT payload snapshot regression detection
- **WHEN** a code change alters an MQTT discovery or state payload's exact shape
- **THEN** the `e2e-test` skill does not fail on this — only real HA entity-state behavior is checked
