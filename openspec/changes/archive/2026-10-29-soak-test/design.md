## Context

njord has an agent-orchestrated E2E test (~81 steps, ~5 minutes) that validates functionality as a point-in-time snapshot. It cannot detect issues that surface over time: polling lifecycle regressions, budget accounting drift, stream disconnects, or persistence recovery failures. The soak test fills this gap with a 4-hour unattended run using a hybrid approach: a bash script collects data, the agent evaluates it.

The existing stack (Docker Compose with njord + Home Assistant + ha-njord) and HA onboarding flow (E2E S0-S2) are reused unchanged.

## Goals / Non-Goals

**Goals:**

- Validate njord stability over 16 poll cycles (4h at 15-minute intervals)
- Verify persistence recovery after container restart (budget, poll timers, entities)
- Detect regressions in polling lifecycle (Discovery -> Learned transition)
- Confirm budget accounting accuracy over multiple cycles
- Produce a structured, agent-evaluatable test report

**Non-Goals:**

- CI integration (local-only, agent-evaluated)
- Mock/stub APIs (real Open-Meteo, same as E2E)
- 24-hour endurance (accelerated polling covers equivalent cycle count)
- Chaos engineering beyond the single planned restart
- Production code changes

## Decisions

### D1: Hybrid collector + agent evaluation (over pure agent or pure script)

The collector is a bash script; the evaluator is the agent reading its output.

**Why**: A pure agent approach (Option A) would bind a Claude session for 4 hours with enormous token cost. A pure script approach (Option B) would need complex evaluation logic in bash. The hybrid gets unattended collection with intelligent evaluation.

**Alternative considered**: Python collector -- rejected because bash+jq has no additional dependencies (curl, jq, grpcurl are already E2E prerequisites), and the collection logic is straightforward sequential HTTP/gRPC calls.

### D2: 4 data sources per tick

Each 5-minute tick collects from:

1. `GET /healthz` (HTTP 8080) -- composite health status as plain text
2. `GET /metrics` (HTTP 8080) -- Prometheus text format with all counters and budget gauges
3. `grpcurl GetStatus` (gRPC 8081) -- poll phase, miss_count, cycle_seconds, enrichment_count, uptime
4. `GET /api/states` (HA 8123) -- entity count, unavailable count, values, last_updated

**Why**: These four cover all observable state. `/metrics` provides budget gauges (`njord_budget_used_monthly` etc.) so grpcurl is only needed for poll lifecycle fields not exposed as metrics. Budget is collected from both sources for cross-validation.

**Alternative considered**: Scraping only `/metrics` + `/healthz` -- rejected because poll phase/miss_count/enrichment_count are only in GetStatus, and entity-level checks require HA API.

### D3: Container restart at T+2h with 2-minute tolerance window

**Why**: T+2h gives 8 steady-state cycles before restart and 8 after, balanced for comparison. The 2-minute window accounts for: njord boot time (~15-30s), HA status coordinator reconnect (~30s), and stream reconnect with exponential backoff (~1-60s).

**Observed restart behavior** (investigated):

- BudgetTracker: persisted, monthly_used identical after restart
- PollWorker: resumes immediately, fires overdue timers (fresh data in ~1-3 min)
- GrpcConsumer: starts with empty cache (enrichments arrive after first post-restart poll)
- ha-njord: auto-reconnects streams with 1s exponential backoff (max 60s), 60s grace before repair issue
- HA entities: weather/domain entities stay at last value (not unavailable), stream sensors briefly "off", status sensors briefly "unavailable"

### D4: PollInterval=15min, DiscoveryInterval=5min (compose override)

**Why**: 15-minute polls yield 16 cycles in 4 hours -- enough to validate lifecycle, budget linearity, and data freshness. 5-minute discovery ensures the Discovery-to-Learned transition completes in ~15 minutes (3 discovery cycles), keeping warmup short.

**Alternative considered**: Default intervals (60min poll, 20min discovery) -- rejected because 4 cycles in 4 hours is insufficient for budget linearity checks and the warmup phase alone would take 60+ minutes.

### D5: JSONL output (one JSON object per line)

**Why**: Each tick is independent; partial files are valid if the script is interrupted. The agent can read and evaluate individual lines. No streaming JSON parser needed.

**Alternative considered**: Single JSON array -- rejected because a crash mid-write corrupts the entire file.

### D6: Phase tracking in the collector

The collector tracks 7 phases: warmup, discovery, steady_state, pre_restart, restarting, recovery, post_restart. Phase transitions are based on elapsed time (restart) and observed state (GetStatus phase field).

**Why**: The evaluator needs to exclude restart-window ticks from stability checks. Phase is determined by the collector (which knows elapsed time and restart triggers), not retroactively by the evaluator.

## Risks / Trade-offs

**[Real API dependency]** -- The test uses the real Open-Meteo API. If the API is down or rate-limited, the test will report failures that are not njord bugs. Mitigation: the event log distinguishes fetch failures from internal errors; the agent evaluator can note API issues.

**[Timing variance]** -- Tick intervals depend on how long each collection cycle takes. A slow grpcurl or HA API response could shift ticks by seconds. Mitigation: the collector uses wall-clock timestamps, not assumed intervals; evaluation checks use the `ts` field, not tick count.

**[Docker restart semantics]** -- `docker restart` sends SIGTERM then SIGKILL after grace period. If njord doesn't shut down cleanly, persistence state could be inconsistent. Mitigation: njord's Akka persistence writes are synchronous; the default 10s stop grace period is sufficient.

**[HA entity registration after restart]** -- ha-njord re-registers entities on reconnect. If HA's config entry cache is stale, entities might appear duplicated briefly. Mitigation: the evaluation checks entity count stability, not identity; duplicates would show as count increase.

## File Layout

```
e2e/
  docker-compose.soak.yml        # compose override (PollInterval, DiscoveryInterval)
  soak/
    SOAK-TEST-PLAN.md             # test plan documentation
    soak-collector.sh             # bash+jq collector script

.claude/skills/soak-test/
  SKILL.md                        # agent skill definition

# Runtime output (gitignored):
e2e/soak/soak-results.jsonl       # structured tick data
e2e/soak/soak-events.log          # milestone and error log
```

## Open Questions

None -- all design decisions resolved during exploration phase.
