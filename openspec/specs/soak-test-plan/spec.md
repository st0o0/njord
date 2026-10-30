## Purpose

The soak-test plan documents the 4-hour stability test design, configuration, evaluation criteria and container restart protocol.

## Requirements

### Requirement: Test plan documentation

A test plan document (`e2e/soak/SOAK-TEST-PLAN.md`) SHALL document the soak test design: purpose, prerequisites, configuration, phases, JSONL schema, pass/fail evaluation matrix, and the container restart protocol.

#### Scenario: Standalone documentation

- **WHEN** an operator reads the soak test plan without prior context
- **THEN** they can understand what the test validates, how to run it, and how to interpret results

### Requirement: Compose override for accelerated polling

A Docker Compose override file (`e2e/docker-compose.soak.yml`) SHALL set `Njord__Polling__PollInterval=00:15:00` and `Njord__Polling__DiscoveryInterval=00:05:00`, composable with the existing `e2e/docker-compose.e2e.yml`.

#### Scenario: Compose file composition

- **WHEN** run with `docker compose -f e2e/docker-compose.e2e.yml -f e2e/docker-compose.soak.yml up`
- **THEN** njord starts with 15-minute poll interval and 5-minute discovery interval, all other E2E configuration preserved

### Requirement: Evaluation criteria

The test plan SHALL define 19 pass/fail checks across 6 categories with severity levels (FAIL or WARN). Checks that are sensitive to restart downtime SHALL be excluded during the 2-minute restart tolerance window.

#### Scenario: Evaluation categories

- **WHEN** the agent evaluates soak test results
- **THEN** it checks: Health (H1-H2), Entity Stability (E1-E4), Budget (B1-B2), Polling Lifecycle (P1-P3), Streams and Metrics (S1, M1-M3), Recovery (R1-R5)

### Requirement: Expected timeline

The test plan SHALL document the expected timeline for a 4-hour run with 15-minute poll interval: 16 poll cycles, Discovery-to-Learned within 15 minutes, container restart at T+2h, recovery within 60 seconds, 48 collector ticks at 5-minute intervals.

#### Scenario: Timeline validation

- **WHEN** the soak test completes a full 4-hour run
- **THEN** the number of ticks is approximately 48 (±2 for timing variance) and the number of observed poll cycles is approximately 16 (±2)
