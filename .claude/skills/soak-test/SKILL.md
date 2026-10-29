---
name: soak-test
description: Run the 4-hour soak test against a real Docker stack (njord + Home Assistant). Validates stability over 16 poll cycles, container restart recovery, budget accuracy, and entity persistence. Produces a PASS/FAIL results report.
---

# Soak Test -- njord + ha-njord

4-hour stability test with accelerated polling, container restart at T+2h,
and automated evaluation. Three phases: Setup, Collection, Evaluation.

Read the full test plan at `e2e/soak/SOAK-TEST-PLAN.md`.

## Prerequisites

- Docker running
- ha-njord cloned at `../ha-njord` relative to repo root
- `curl`, `jq`, `grpcurl` available
- Chrome browser (optional, for HA onboarding)

## Phase Detection

Before starting, check if results already exist:

```bash
ls e2e/soak/soak-results.jsonl 2>/dev/null
```

- **If results exist**: offer to evaluate existing results (Phase 3 only) or start fresh
- **If no results**: start from Phase 1

## Phase 1: Setup

Reuse the E2E test's stack setup flow (S0-S2 from `e2e/E2E-TEST-PLAN.md`):

### S0 -- Stack Start

```bash
cd <repo-root>
docker compose -f e2e/docker-compose.e2e.yml -f e2e/docker-compose.soak.yml down -v
docker compose -f e2e/docker-compose.e2e.yml -f e2e/docker-compose.soak.yml up -d --build
```

Poll for health:
- njord: `curl http://localhost:8080/alive` (timeout 60s)
- HA: `curl http://localhost:8123/` (timeout 120s)

### S1 -- HA Onboarding + Token

Follow the same flow as E2E S1: onboarding, user creation, long-lived access token.
Save the token -- it's needed for the collector.

### S2 -- Integration Setup

Add njord integration via HA API (same as E2E S2).

### S3 -- Wait for Entities

Poll `/api/states` until `weather.barcelona` appears (timeout 120s).

## Phase 2: Collection

Start the collector in background:

```bash
bash e2e/soak/soak-collector.sh --ha-token "<token>" &
```

Tell the user:
> Collector started. It will run for 4 hours, collecting data every 5 minutes.
> Container restart happens at T+2h automatically.
> Re-invoke `/soak-test` when the collector completes to evaluate results.

The collector runs unattended. Monitor progress with:
```bash
tail -f e2e/soak/soak-events.log
```

## Phase 3: Evaluation

Read `e2e/soak/soak-results.jsonl` and apply all 19 pass/fail checks.

### Evaluation Procedure

1. Read all lines from `soak-results.jsonl` into an array of tick objects
2. Read `soak-events.log` for restart timing (RESTART events)
3. Apply each check below
4. Generate report

### Check Definitions

For each check: the ID, JSONL fields to inspect, condition, restart-window handling,
and what to cite as evidence on failure.

#### Health (H1, H2)

- **H1 (FAIL)**: For every tick where `in_restart_window == false`:
  `health.status` must not be `"Unhealthy"` (or `"unreachable"` outside restart).
  Evidence: tick number, timestamp, status value.

- **H2 (WARN)**: Count ticks where `health.status == "Degraded"` (excl. restart window).
  WARN if count > 2. Evidence: tick numbers and timestamps.

#### Entity Stability (E1-E4)

- **E1 (FAIL)**: `entities.total` must be the same value in every tick where
  `entities.reachable == true`. Compare all values; ignore ticks where HA was
  unreachable. Evidence: tick numbers with differing counts.

- **E2 (FAIL)**: For every tick where `in_restart_window == false` and
  `entities.reachable == true`: `entities.unavailable_count == 0`.
  Evidence: tick numbers and `unavailable_ids`.

- **E3 (FAIL)**: For every tick where `in_restart_window == false` and
  `entities.reachable == true`: `entities.stalest_age_min < 30`.
  Evidence: tick number, stalest entity, age.

- **E4 (WARN)**: Same as E3 but threshold is 20 minutes.

#### Budget (B1, B2)

- **B1 (FAIL)**: `budget.monthly_used` must never decrease between consecutive ticks
  (where both have budget data). Evidence: tick pair and values.

- **B2 (WARN)**: At the final tick, compute expected budget:
  `expected = (elapsed_min / poll_interval_min) * 11` where poll_interval_min = 15.
  Actual must be within 15% of expected: `abs(actual - expected) / expected <= 0.15`.
  Evidence: actual vs expected values.

#### Polling Lifecycle (P1-P3)

- **P1 (FAIL)**: Find first tick where `grpc_status.phase == "POLL_PHASE_STEADY"`.
  Its `elapsed_min` must be <= 30. Evidence: first STEADY tick or "never reached".

- **P2 (WARN)**: For ticks where `in_restart_window == false` and phase is
  `steady_state` or `post_restart`: `grpc_status.miss_count <= 2`.
  Evidence: tick numbers with high miss counts.

- **P3 (FAIL)**: For ticks where `in_restart_window == false` and
  `grpc_status.reachable == true`: `grpc_status.enrichment_count == 66`.
  Evidence: tick numbers with wrong count.

#### Streams and Metrics (S1, M1-M3)

- **S1 (FAIL)**: For ticks where `in_restart_window == false` and
  `entities.reachable == true`: `entities.stream_domain == "on"` and
  `entities.stream_enrichment == "on"`. Evidence: tick numbers and states.

- **M1 (WARN)**: `metrics.pipeline_failed_total` must not increase between
  consecutive ticks. Evidence: tick pair and delta.

- **M2 (WARN)**: At the final tick: `metrics.pipeline_failed_total == 0`.
  Evidence: final value.

- **M3 (WARN)**: Between consecutive non-restart ticks,
  `metrics.pipeline_events_total` must increase (pipeline is producing events).
  Allow up to 3 stalled intervals. Evidence: tick pairs with zero growth.

#### Recovery (R1-R5)

Parse `soak-events.log` for RESTART entries to get recovery_seconds and
pre/post state.

- **R1 (FAIL)**: Recovery time (from RESTART log) must be < 60 seconds.
  Evidence: recovery_seconds value.

- **R2 (FAIL)**: `budget.monthly_used` in the first post-restart tick with budget
  data must equal the pre-restart value (from RESTART log or last pre-restart tick).
  Evidence: pre and post values.

- **R3 (FAIL)**: `entities.total` in the first post-restart tick (where
  `entities.reachable == true`) must equal the pre-restart count.
  Evidence: pre and post counts.

- **R4 (WARN)**: Find first post-restart tick where both stream sensors are "on".
  Its time since restart must be < 90 seconds (calculate from tick timestamps
  and restart event timestamp). Evidence: reconnect time or "not reconnected".

- **R5 (WARN)**: `metrics.pipeline_events_total` must increase within one poll
  interval (15 min) after restart. Compare first post-restart tick to a tick
  15 min later. Evidence: event counts.

### Report Format

Generate a structured report:

```
## Soak Test Results

**Duration:** 4h 0m | **Ticks:** 48 | **Poll cycles:** ~16
**Overall: PASS** (or WARN / FAIL)

### Health (2/2 passed)
- [PASS] H1: Never Unhealthy
- [PASS] H2: Degraded <= 2 ticks

### Entity Stability (4/4 passed)
- [PASS] E1: Entity count constant (70)
- [PASS] E2: Zero unavailable
- [PASS] E3: No stale entities (max 12 min)
- [PASS] E4: Freshness warning (max 12 min)

### Budget (2/2 passed)
- [PASS] B1: Monotonic increase
- [PASS] B2: Final within range (actual: 172, expected: 176, delta: 2.3%)

### Polling Lifecycle (3/3 passed)
- [PASS] P1: STEADY reached at 12 min
- [PASS] P2: Max miss count: 0
- [PASS] P3: All enrichments (66)

### Streams and Metrics (4/4 passed)
- [PASS] S1: Streams always connected
- [PASS] M1: No fetch failures
- [PASS] M2: No pipeline failures
- [PASS] M3: Pipeline active

### Recovery (5/5 passed)
- [PASS] R1: Recovery in 24s
- [PASS] R2: Budget preserved (132 -> 132)
- [PASS] R3: Entities re-registered (70 -> 70)
- [PASS] R4: Streams reconnected in 35s
- [PASS] R5: Fresh data within 3 min

### Summary
- 19/19 checks passed (10 FAIL-severity, 9 WARN-severity)
- Container restart: 24s recovery, state fully preserved
- Budget accuracy: 172 actual vs 176 expected (2.3% delta)
```

For failed checks, add evidence below the line:
```
- [FAIL] E2: Zero unavailable
  Evidence: tick 25 (T+2h05) had 3 unavailable: [server_monthly_usage, server_uptime, server_version]
```

## Teardown

After evaluation, offer to tear down:

```bash
docker compose -f e2e/docker-compose.e2e.yml -f e2e/docker-compose.soak.yml down -v
```
