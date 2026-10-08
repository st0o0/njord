## Why

The current E2E test plan covers 44 steps across 8 phases — enough to prove the stack starts and entities register, but shallow compared to the FunkArr benchmark (~170+ steps, 29 sections). Critical behaviour goes unverified: most gRPC RPCs are never called, entity attributes are checked for existence but not correctness, enrichment computations are not validated against expected values, error handling is untested, and multi-poll-cycle behaviour is never exercised. Expanding coverage now catches integration regressions that unit tests cannot reach.

## What Changes

- **gRPC API exhaustive coverage** — test every RPC across all 4 services (WeatherService, OpsService, AdminService, SensorService), not just GetStatus/GetBudgetStatus. Includes streaming RPCs and error responses.
- **Entity attribute depth** — validate actual attribute values, units, device classes, and state classes against the entity grid spec, not just entity existence.
- **Enrichment computation validation** — verify consensus agreement/spread, alert severity/confidence values, index ranges, derived value formulas against known inputs.
- **Error handling** — invalid gRPC requests (bad location, unknown model, malformed sensor readings), edge-case API responses.
- **Multi-poll-cycle testing** — trigger 2+ poll cycles, verify data freshness and state updates.
- **Setup recipes** — named configuration scenarios (single model, disabled enrichments, custom horizons) with deterministic state setup.
- **HA browser verification** — entity cards in HA UI showing correct state, not just API-level existence.
- **Config change resilience** — hot-reload via AdminService SetLocations/SetSettings, verify entity set updates.
- **Teardown verification** — integration removal, entity cleanup.
- **Parallel subagent expansion** — 3 Haiku subagents (matching FunkArr pattern) for API, enrichment, and gRPC validation.

## Non-goals

- Testing Open-Meteo API reliability or accuracy (external dependency, non-deterministic).
- Testing HA core functionality (onboarding, automations, Lovelace editor) beyond njord integration.
- Performance benchmarking or load testing.
- MQTT-path E2E (the stack uses gRPC via ha-njord; MQTT egress is tested separately).

## Capabilities

### New Capabilities

- `e2e-grpc-coverage`: Exhaustive gRPC API testing across all 4 services (16 RPCs), including streaming, error responses, and admin mutations.
- `e2e-entity-depth`: Deep entity attribute validation — values, units, device classes, state classes against the entity grid.
- `e2e-enrichment-validation`: Enrichment computation verification — consensus, alerts, indices, derived values, trends, history against expected ranges.
- `e2e-error-handling`: Error handling and edge-case testing for gRPC and HA REST API.
- `e2e-multi-cycle`: Multi-poll-cycle behaviour testing — data freshness, state transitions, budget tracking across cycles.
- `e2e-config-scenarios`: Setup recipes and configuration scenario testing (single model, disabled enrichments, custom horizons, hot-reload).
- `e2e-ha-browser`: HA UI browser verification of entity cards and state display.
- `e2e-teardown`: Integration removal and entity cleanup verification.

### Modified Capabilities

- `e2e-test-plan`: Restructure from 8 phases / 44 steps to ~25+ sections / 150+ steps, add setup recipes, expand orchestration to 3 parallel subagents.
- `e2e-skill`: Update skill to handle new phases, setup recipes, and 3-subagent orchestration.

## Impact

- **Files:** `e2e/E2E-TEST-PLAN.md` (major rewrite), `.claude/skills/e2e-test/SKILL.md` (orchestration update), `e2e/docker-compose.e2e.yml` (possible config variants).
- **Dependencies:** grpcurl (already used), curl/HA REST API (already used), claude-in-chrome (already used).
- **No production code changes** — this is purely test infrastructure.
- **API budget:** No additional polling load; E2E runs are manual, infrequent, and use the same 2-model config.
