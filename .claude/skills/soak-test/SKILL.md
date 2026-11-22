---
name: soak-test
description: Run the soak test against a real Docker stack (njord + Home Assistant). Two profiles -- default (4h accelerated) and realcycle (48h production intervals). Validates stability, container restart recovery, budget accuracy, and entity persistence. Produces a PASS/FAIL results report.
---

# Soak Test -- njord + ha-njord

Stability test with container restart and automated evaluation.
Two profiles: default (4h accelerated) and realcycle (48h production intervals).
Three phases: Setup, Collection, Evaluation.

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

## Profile Selection

Ask the user which profile to use if not specified:
- **default**: 4h, accelerated polling (15min), restart at 2h, compose: `docker-compose.soak.yml`
- **realcycle**: 48h, production intervals (1h-24h), restart at 20h, compose: `docker-compose.realcycle.yml`

## Phase 1: Setup

Reuse the E2E test's stack setup flow (S0-S2 from `e2e/E2E-TEST-PLAN.md`):

### S0 -- Stack Start

For default profile:
```bash
cd <repo-root>
docker compose -f e2e/docker-compose.e2e.yml -f e2e/docker-compose.soak.yml down -v
docker compose -f e2e/docker-compose.e2e.yml -f e2e/docker-compose.soak.yml up -d --build
```

For realcycle profile:
```bash
cd <repo-root>
docker compose -f e2e/docker-compose.e2e.yml -f e2e/docker-compose.realcycle.yml down -v
docker compose -f e2e/docker-compose.e2e.yml -f e2e/docker-compose.realcycle.yml up -d --build
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

For default profile:
```bash
bash e2e/soak/soak-collector.sh --ha-token "<token>" &
```

For realcycle profile:
```bash
bash e2e/soak/soak-collector.sh --ha-token "<token>" --profile realcycle &
```

Tell the user:
> Collector started. It will run for {4 hours / 48 hours} depending on profile.
> Container restart happens at {T+2h / T+20h} automatically.
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

- **E2 (FAIL)**: No **new** unavailable entities. Many entities are unavailable by
  design (alerts that don't fire, features with missing domain data).
  Establish a **baseline unavailable set** from tick 5+ (first tick with
  `entities.reachable == true` and `unavailable_count > 0`). For subsequent ticks,
  FAIL only if a new entity_id appears in `unavailable_ids` that was NOT in the
  baseline. Evidence: new unavailable IDs and tick numbers.

- **E3 (FAIL)**: For every tick where `in_restart_window == false` and
  `entities.reachable == true`:
  - If `staleness_thresholds` is present AND `domain_staleness` values are non-zero
    (realcycle profile): each `entities.domain_staleness.<d>` must be <
    `staleness_thresholds.<d>`. Skip domains with 0 available entities.
  - Otherwise (default profile): `entities.stalest_age_min < 30`.
  Evidence: tick number, domain, stalest entity/age, threshold.

- **E4 (WARN)**: Same as E3 but at 80% of the threshold (default: 20 min;
  realcycle: 80% of per-domain threshold).

#### Budget (B1, B2)

- **B1 (FAIL)**: `budget.monthly_used` must never decrease between consecutive ticks
  (where both have budget data). Evidence: tick pair and values.

- **B2 (WARN)**: At the final tick, compute expected budget.
  - Default: `expected = (elapsed_min / 15) * 11`. Actual within 15%.
  - Realcycle: `expected = elapsed_hours * 3.4` (empirical). Actual within 20%.
  Evidence: actual vs expected values.

#### Polling Lifecycle (P1-P3)

- **P1 (FAIL)**: Polling must become active promptly.
  - Default: first tick with `POLL_PHASE_STEADY`, `elapsed_min <= 30`.
  - Realcycle: first tick with `POLL_PHASE_ACTIVE`, `elapsed_min <= 10`. STEADY may
    take >48h with slow domains; ACTIVE is sufficient.
  Evidence: first matching tick or "never reached".

- **P2 (WARN)**: For ticks where `in_restart_window == false` and phase is
  `steady_state` or `post_restart`: `grpc_status.miss_count <= 2`.
  Evidence: tick numbers with high miss counts.

- **P3 (FAIL)**: For ticks where `in_restart_window == false` and
  `grpc_status.reachable == true`: `grpc_status.enrichment_count` must be **constant**
  (same value in every tick). Do not hardcode 65 -- the count depends on enabled
  features (currently 88-89). Evidence: tick numbers with differing counts.

#### Streams and Metrics (S1, M1-M3)

- **S1 (WARN/FAIL)**: For ticks where `in_restart_window == false` and
  `entities.reachable == true`: check `entities.stream_domain` and
  `entities.stream_enrichment`.
  - `"on"`: PASS. `"off"`: FAIL (stream dropped).
  - `"missing"`: WARN (sensor not registered in this ha-njord version; entity
    naming may differ). Do not FAIL on "missing".
  Evidence: tick numbers and states.

- **M1 (WARN)**: `metrics.pipeline_failed_total` must not increase between
  consecutive ticks. Evidence: tick pair and delta.

- **M2 (WARN)**: At the final tick: `metrics.pipeline_failed_total == 0`.
  Evidence: final value.

- **M3 (WARN)**: Pipeline must produce events regularly.
  - Default: `pipeline_events_total` must increase between consecutive non-restart
    ticks. Allow up to 3 stalled intervals total.
  - Realcycle: with 1h poll intervals and 15m ticks, most inter-poll ticks show no
    growth. Allow up to `ceil(poll_interval / collection_interval)` **consecutive**
    stalled ticks (realcycle: 4). WARN only if a consecutive stall run exceeds this.
  Evidence: longest consecutive stall run and tick range.

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
  and restart event timestamp). If stream sensors are "missing" (not registered),
  skip this check with a note. Evidence: reconnect time or "not reconnected".

- **R5 (WARN)**: `metrics.pipeline_events_total` must increase within one poll
  interval after restart (default: 15 min; realcycle: 60 min, the fastest domain).
  Compare first post-restart tick to a tick one interval later. Evidence: event counts.

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
- [PASS] E1: Entity count constant (91)
- [PASS] E2: No new unavailable (baseline: 40, no new)
- [PASS] E3: No stale entities (max 12 min)
- [PASS] E4: Freshness warning (max 12 min)

### Budget (2/2 passed)
- [PASS] B1: Monotonic increase
- [PASS] B2: Final within range (actual: 172, expected: 176, delta: 2.3%)

### Polling Lifecycle (3/3 passed)
- [PASS] P1: Polling active at 0 min
- [PASS] P2: Max miss count: 0
- [PASS] P3: Enrichments stable (88 in all ticks)

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
- [FAIL] E2: No new unavailable
  Evidence: tick 25 (T+2h05) has 3 NEW unavailable (not in baseline): [sensor.x, sensor.y, sensor.z]
```

## Teardown

After evaluation, offer to tear down:

```bash
# default profile
docker compose -f e2e/docker-compose.e2e.yml -f e2e/docker-compose.soak.yml down -v
# realcycle profile
docker compose -f e2e/docker-compose.e2e.yml -f e2e/docker-compose.realcycle.yml down -v
```
