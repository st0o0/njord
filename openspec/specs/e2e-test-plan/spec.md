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

