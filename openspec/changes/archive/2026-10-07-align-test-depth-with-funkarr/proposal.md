## Why

njord's test suite covers unit and actor-level concerns well but lacked two
things FunkArr has proven valuable: persistence roundtrip tests (Verify
`_shape()` snapshots alone miss deserialization bugs) and a way to verify the
full data flow from API response to MQTT/HA state, beyond what unit and actor
tests can reach.

## What Changes

- **Persistence roundtrip tests**: Added `_roundtrip()` tests alongside existing Verify `_shape()` tests for every persistence DTO. Serialize via Newtonsoft.Json (the Akka persistence serializer), deserialize back, assert field-level equality.
- **End-to-end verification**: instead of a `Njord.E2E.Tests` Testcontainers project (the originally proposed approach), built the `e2e-test` Claude Code skill — an agent-orchestrated test that boots the real stack via `docker-compose.yml` (njord + ha-njord + Home Assistant + Mosquitto), drives Home Assistant through its real REST API and UI (via browser automation), and asserts against actual entity states. Covers: entity registration, forecast data, enrichment entities, connectivity/server diagnostics, direct gRPC, a container restart (resilience), and a manual poll trigger. Results are written to `e2e/results/E2E-TEST-RESULTS-<date>.md`.
- **Integration test fixture**: built as `NjordFixture` (TestServer + TestProbes) in `Njord.IntegrationTests`, then the project was deleted outright in a later, separate decision (2026-10-07) once the host moved to domain-specific Servus setup containers and sharded snapshot actors — see Non-goals.

## Non-goals

- Changing production code behavior — this is test infrastructure only.
- **Rebuilding `NjordFixture`/`Njord.IntegrationTests`**: it was built as part of this change, then deliberately deleted in a later, unrelated decision. Not reinstated here; if host-level integration tests are wanted again they need a fresh design against the current sharded-actor host, which is out of scope for this change.
- **A `Njord.E2E.Tests` Testcontainers project with Verify-snapshotted MQTT payloads**: superseded by the `e2e-test` skill. The skill verifies more of the real system (actual Home Assistant, not simulated MQTT subscribers) but trades CI automation and byte-exact payload regression detection for an on-demand, agent-driven run. If automated MQTT-payload regression detection in CI is needed later, that is a new, narrower proposal — not this one.
- Testing against a real Open-Meteo API endpoint (the skill uses the real stack but Open-Meteo itself is out of scope for regression testing).

## Capabilities

### New Capabilities
- `persistence-roundtrip-tests`: Serialize/deserialize roundtrip tests for every persistence DTO alongside existing Verify snapshots
- `e2e-stack-verification`: Agent-orchestrated, Docker-Compose-based end-to-end verification of the real njord + ha-njord + Home Assistant stack

### Modified Capabilities
- `test-project-structure`: Existing spec updated to reflect roundtrip tests, the absence of `Njord.IntegrationTests`/`Njord.E2E.Tests`, and the `e2e-test` skill

## Impact

- **Modified project**: `src/Njord.Persistence.Tests/` — added roundtrip tests
- **Added**: `.claude/skills/e2e-test/SKILL.md`, `e2e/E2E-TEST-PLAN.md`, `e2e/docker-compose.e2e.yml`
- **Not added**: `src/Njord.E2E.Tests/` (superseded, see Non-goals)
- **Not reinstated**: `src/Njord.IntegrationTests/` (deleted in a separate decision, see Non-goals)
- **No API-budget impact**: no polling changes
