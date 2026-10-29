## Why

The existing E2E test is a snapshot: it starts the stack, validates entities and gRPC once, and tears down in ~5 minutes. It cannot detect stability issues that only surface over time -- memory leaks, stream disconnects, polling lifecycle regressions, budget drift, or persistence recovery failures after a container restart. A 4-hour soak test with accelerated polling (15 min) gives 16 natural poll cycles, enough to validate steady-state behavior, the Discovery-to-Learned transition, and a full container restart with persistence recovery.

## What Changes

- Add a **bash+jq collector script** (`e2e/soak/soak-collector.sh`) that runs alongside the Docker stack and samples four data sources every 5 minutes: `/healthz`, `/metrics` (Prometheus), gRPC `GetStatus`, and HA `/api/states`. Output is structured JSONL + a human-readable event log.
- Add a **compose override** (`e2e/docker-compose.soak.yml`) that sets `PollInterval=15min` and `DiscoveryInterval=5min`.
- Add a **soak test plan** (`e2e/soak/SOAK-TEST-PLAN.md`) documenting the 5 phases (warmup, discovery, steady-state, restart, post-restart), the JSONL schema, and 19 pass/fail evaluation criteria.
- Add a **soak-test agent skill** (`.claude/skills/soak-test/SKILL.md`) that orchestrates: stack setup (reuses E2E S0-S2 flow), collector start, and post-run evaluation against the JSONL data.
- The collector triggers a **container restart at T+2h** to validate persistence recovery (BudgetTracker state, PollWorker timers, HA entity re-registration).

## Non-goals

- CI integration -- this is a local-only test, run on demand.
- Testing against mock/fake Open-Meteo -- the soak test uses the real API (same as the existing E2E).
- 24-hour endurance variant -- the 4h soak with accelerated polling covers the same ground. Can be added later.
- Chaos testing beyond the single planned container restart.
- No changes to production code -- this is purely test infrastructure.

## API Budget Impact

With `PollInterval=15min`, 1 location, all 66 features enabled (same as E2E config): 16 cycles x 11 call weight = **176 calls per 4h run**. Negligible against the 300k/month free tier. Even daily runs would add ~1,320/month.

## Capabilities

### New Capabilities

- `soak-test-collector`: Bash+jq script that periodically samples njord health, metrics, gRPC status, and HA entity state, outputs structured JSONL, and orchestrates a container restart at a configurable time.
- `soak-test-plan`: Test plan documenting phases, JSONL schema, pass/fail evaluation matrix (19 checks across 6 categories), and the container restart protocol.
- `soak-test-skill`: Agent skill that orchestrates the full soak test lifecycle -- stack setup, collector execution, JSONL evaluation, and PASS/FAIL report generation.

### Modified Capabilities

(none -- no existing spec requirements change)

## Impact

- **New files only**: `e2e/soak/` directory, compose override, agent skill. No production code changes.
- **Dependencies**: `curl`, `jq`, `grpcurl`, Docker -- all already required by the existing E2E test.
- **Existing E2E**: untouched. The soak test reuses the same Docker Compose base and HA onboarding flow.
