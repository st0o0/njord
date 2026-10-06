## Why

njord and ha-njord (the Home Assistant custom component) have no integration test that exercises the full gRPC path end-to-end: njord polling Open-Meteo → gRPC streaming → ha-njord coordinator → Home Assistant entities. Unit tests and Verify snapshots cover serialization and individual actors, but the cross-repo, cross-process interaction is untested. The MQTT path may become obsolete in favor of gRPC, making this the primary integration surface to validate.

The approach follows the FunkArr model: an agent-orchestrated E2E test run against a real Docker stack, executed by Claude agents (Opus + parallel Haiku subagents) rather than a classical test framework.

## What Changes

- Add `e2e/` directory with a Docker Compose stack (njord + Home Assistant with ha-njord mounted), an E2E test plan document, and a Claude Code skill to execute it
- The E2E test plan covers 8 phases: stack setup, HA integration config flow (browser), entity registration, forecast validation, enrichment validation, connectivity checks, direct gRPC validation, and resilience (restart/reconnect)
- The same `e2e/` directory is synced to ha-njord (D:\GIT\ha-njord); njord is the leading repo
- No production code changes — this is infrastructure/tooling only

## Non-goals

- Replacing existing unit tests, Verify snapshot tests, or Testcontainers integration tests
- Testing the MQTT discovery path (gRPC-only focus)
- Automated CI execution — this is a developer-triggered, agent-driven test run
- Mock/stub server for Open-Meteo — tests use the real API (1-2 requests per run)

## Capabilities

### New Capabilities

- `e2e-test-plan`: The test plan document (E2E-TEST-PLAN.md) defining 8 phases with numbered steps, expected entities (~47), orchestration model (Opus main + Haiku subagents), and PASS/FAIL result format
- `e2e-docker-stack`: Docker Compose stack for E2E testing (njord build + HA stable image with ha-njord volume-mounted, gRPC-only, minimal config with all enrichments)
- `e2e-skill`: Claude Code skill that executes the E2E test plan, manages Docker lifecycle, orchestrates browser and API tests, spawns parallel subagents, and produces a results document

### Modified Capabilities

(none — no existing spec-level behavior changes)

## Impact

- New files in `e2e/` directory (not in `src/`, no impact on build/test)
- New Claude Code skill (`.claude/` or project-level skill definition)
- ha-njord repo receives a copy of the `e2e/` directory
- No changes to production code, project references, or CI pipeline
- API budget: no additional polling beyond what njord already does — the E2E test triggers one normal poll cycle (2 requests for 2 models × 1 location = 2 requests)
