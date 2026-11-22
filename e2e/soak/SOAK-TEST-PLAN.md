# Soak Test Plan -- njord

4-hour stability test with accelerated polling (15 min), container restart at T+2h,
and automated data collection for agent evaluation.

## Purpose

Validate njord stability over time: polling lifecycle (Discovery -> Learned), budget
accounting accuracy, stream connectivity, persistence recovery after container restart,
and entity stability in Home Assistant. Complements the snapshot E2E test which validates
functionality at a single point in time.

## Prerequisites

- Docker running
- ha-njord cloned at `../ha-njord` relative to repo root
- `curl`, `jq`, `grpcurl` available on PATH
- grpcurl proto path: `protos/njord/v2/ops.proto` (from repo root)

## Configuration

### Compose Override

`e2e/docker-compose.soak.yml` overrides polling intervals:

```yaml
services:
  njord:
    environment:
      - Njord__Polling__PollInterval=00:15:00
      - Njord__Polling__Domains__Forecast__PollInterval=00:15:00
      - Njord__Polling__Domains__AirQuality__PollInterval=00:15:00
```

Start with:
```bash
docker compose -f e2e/docker-compose.e2e.yml -f e2e/docker-compose.soak.yml up -d --build
```

### Default Parameters

| Parameter | Default | Description |
|-----------|---------|-------------|
| `--duration` | `4h` | Total test duration |
| `--interval` | `5m` | Data collection interval |
| `--restart-at` | `2h` | Container restart time |
| `--ha-token` | (required) | HA long-lived access token |
| `--njord-url` | `http://localhost:8080` | njord HTTP endpoint |
| `--grpc-host` | `localhost:8081` | njord gRPC endpoint |
| `--ha-url` | `http://localhost:8123` | Home Assistant endpoint |
| `--container` | `njord-e2e` | Docker container name |

### Expected Numbers (4h, PollInterval=15m, 1 location, all 89 features)

- Poll cycles: ~16
- Collector ticks: ~48 (at 5-min intervals)
- API calls: ~176 (16 cycles x 11 call weight)
- Budget impact: negligible (0.06% of 300k monthly)

## Phases

| Phase | Timeframe | Trigger | What happens |
|-------|-----------|---------|-------------|
| `warmup` | T+0 to ~T+5m | Start | Stack is up, waiting for first poll |
| `active` | ~T+5m to T+2h | `fetch_total > 0` | All domains polling at configured intervals |
| `pre_restart` | T+2h | `elapsed >= restart_at` | Snapshot state, trigger restart |
| `restarting` | T+2h to T+2h+~30s | `docker restart` issued | Container down, /alive polled |
| `recovery` | T+2h+~30s to T+2h+~60s | `/alive` responds | Waiting for HA reconnection |
| `post_restart` | T+2h+~60s to T+4h | Post-restart validation done | Same as active, verifying recovery |
| `complete` | T+4h | Final tick | Test done |

## Data Sources (per tick)

| Source | Endpoint | Data collected |
|--------|----------|---------------|
| Health | `GET /healthz`, `GET /alive` | Status text, HTTP code, liveness |
| Metrics | `GET /metrics` | Prometheus counters (fetch, poll, pipeline) + budget gauges |
| gRPC | `GetStatus` on `OpsService` | Poll phase, miss count, cycle seconds, enrichment count, uptime |
| HA | `GET /api/states` | Entity count, unavailable count, values, stalest age |

## JSONL Schema

Each line in `soak-results.jsonl` is a JSON object:

```json
{
  "tick": 1,
  "ts": "2026-10-29T10:05:00Z",
  "elapsed_min": 5,
  "phase": "warmup",
  "in_restart_window": false,
  "health": {
    "status": "Healthy",
    "http_code": 200,
    "alive": true
  },
  "metrics": {
    "fetch_total": 6,
    "polls_total": 6,
    "cycle_learned_total": 0,
    "pipeline_events_total": 42,
    "pipeline_skipped_total": 0,
    "pipeline_failed_total": 0,
    "data_changed_total": 6
  },
  "budget": {
    "monthly_used": 11,
    "daily_used": 11,
    "monthly_limit": 300000,
    "daily_limit": 10000
  },
  "grpc_status": {
    "reachable": true,
    "uptime_seconds": 300,
    "phase": "POLL_PHASE_ACTIVE",
    "miss_count": 0,
    "cycle_seconds": 0,
    "enrichment_count": 65,
    "budget_partial": false,
    "models_partial": false
  },
  "entities": {
    "reachable": true,
    "total": 70,
    "unavailable_count": 0,
    "unavailable_ids": [],
    "weather_state": "partlycloudy",
    "temperature": 18.3,
    "stream_domain": "on",
    "stream_enrichment": "on",
    "alerts_on": ["uv"],
    "alerts_on_count": 1,
    "alerts_off_count": 21,
    "stalest_entity": "sensor.barcelona_river_discharge",
    "stalest_age_min": 4
  },
  "errors": []
}
```

## Pass/Fail Evaluation Matrix

19 checks across 6 categories. Ticks within the restart tolerance window
(`in_restart_window: true`) are excluded from checks marked "excl. restart".

Checks marked **[profile-aware]** behave differently for `default` vs `realcycle`.

### Health

| ID | Check | Condition | Severity | Restart |
|----|-------|-----------|----------|---------|
| H1 | Never Unhealthy | `health.status != "Unhealthy"` in all ticks | FAIL | excl. restart |
| H2 | Degraded rare | `health.status == "Degraded"` in <= 2 ticks | WARN | excl. restart |

### Entity Stability

| ID | Check | Condition | Severity | Restart |
|----|-------|-----------|----------|---------|
| E1 | Entity count constant | `entities.total` same across all ticks | FAIL | all ticks |
| E2 | No new unavailable **[profile-aware]** | See below | FAIL | excl. restart |
| E3 | No stale entities **[profile-aware]** | See below | FAIL | excl. restart |
| E4 | Freshness warning **[profile-aware]** | See below | WARN | excl. restart |

**E2 details:** Many entities are "unavailable" by design -- alerts that don't fire in
the current season/location (e.g. Frost/Snow/Heat in Barcelona November), features whose
domain data is missing (Flood discharge, Ocean currents). These are structurally unavailable
and not a failure.

- Establish a **baseline unavailable set** from the first tick after warmup where
  `entities.reachable == true` and `entities.unavailable_count > 0` (or tick 5+, whichever
  is later). Record the `unavailable_ids` from that tick.
- For all subsequent ticks: FAIL only if a **new** entity_id appears in `unavailable_ids`
  that was NOT in the baseline set. An entity that was unavailable from the start staying
  unavailable is expected.
- Evidence: the new unavailable entity_ids not in baseline, tick number.

**E3/E4 details:**
- **default profile**: `entities.stalest_age_min < 30` (FAIL), `< 20` (WARN).
- **realcycle profile**: Per-domain thresholds from `staleness_thresholds` in the JSONL.
  Each `entities.domain_staleness.<d>` must be < `staleness_thresholds.<d>` (FAIL),
  < 80% of threshold (WARN). Domains with 0 available entities are skipped.

### Budget

| ID | Check | Condition | Severity | Restart |
|----|-------|-----------|----------|---------|
| B1 | Monotonic increase | `budget.monthly_used` never decreases between ticks | FAIL | all ticks |
| B2 | Final within range **[profile-aware]** | See below | WARN | end only |

**B2 details:**
- **default profile**: `expected = (elapsed_min / 15) * 11`. Actual within 15%.
- **realcycle profile**: `expected = elapsed_hours * 3.4` (empirical: ~165 calls / 48h).
  For a precise formula: `forecast_cycles * ~3 + aq_cycles * ~1 + (hours/6) * ~4 (marine)
  + (hours/6) * ~4 (ensemble) + seasonal_calls + flood_calls`. Actual within 20%
  (wider tolerance due to variable domain timing).

### Polling Lifecycle

| ID | Check | Condition | Severity | Restart |
|----|-------|-----------|----------|---------|
| P1 | Polling active **[profile-aware]** | See below | FAIL | warmup only |
| P2 | Low miss count | `grpc_status.miss_count <= 2` in steady state | WARN | excl. restart |
| P3 | Enrichments stable | `grpc_status.enrichment_count` constant across all ticks | FAIL | excl. restart |

**P1 details:**
- **default profile**: `POLL_PHASE_STEADY` reached within 30 min.
- **realcycle profile**: `POLL_PHASE_ACTIVE` reached within 10 min. STEADY may take
  longer than 48h with slow domains (Seasonal/Flood 24h cycles), so ACTIVE is sufficient.

**P3 details:** The enrichment count depends on the number of enabled features (currently
89). Do not hardcode 65. Instead verify that the count is **constant** across all reachable
ticks -- any change indicates a feature registration problem.

### Streams and Metrics

| ID | Check | Condition | Severity | Restart |
|----|-------|-----------|----------|---------|
| S1 | Streams connected | See below | WARN | excl. restart |
| M1 | No fetch failures | `metrics.pipeline_failed_total` does not increase | WARN | all ticks |
| M2 | No pipeline failures | `metrics.pipeline_failed_total == 0` at final tick | WARN | all ticks |
| M3 | Pipeline active **[profile-aware]** | See below | WARN | excl. restart |

**S1 details:** Check `entities.stream_domain` and `entities.stream_enrichment`.
- State `"on"`: PASS.
- State `"off"`: FAIL (stream was connected but dropped).
- State `"missing"`: WARN (sensor not registered in this HA/ha-njord version; entity
  naming may differ). Do not FAIL on "missing".

**M3 details:**
- **default profile**: `pipeline_events_total` must increase between consecutive non-restart
  ticks. Allow up to 3 stalled intervals total.
- **realcycle profile**: With 1h poll intervals and 15m collection ticks, most ticks between
  polls have no event growth. Allow up to `ceil(poll_interval_min / interval_min)` consecutive
  stalled ticks (realcycle: `ceil(60/15) = 4`). WARN only if a **consecutive** stall run
  exceeds this window.

### Recovery

| ID | Check | Condition | Severity | Restart |
|----|-------|-----------|----------|---------|
| R1 | Recovery time | `/alive` within 60s of restart (from event log) | FAIL | restart only |
| R2 | Budget preserved | `budget.monthly_used` identical pre/post restart | FAIL | restart only |
| R3 | Entities re-registered | `entities.total` same post-restart within 120s | FAIL | restart only |
| R4 | Streams reconnected | Stream sensors "on" within 90s of restart | WARN | restart only |
| R5 | Fresh data within cycle **[profile-aware]** | See below | WARN | restart only |

**R5 details:**
- **default profile**: `pipeline_events_total` grows within 15 min of restart.
- **realcycle profile**: `pipeline_events_total` grows within 60 min of restart
  (the fastest domain interval).

### Overall Result

- **PASS**: 0 FAILs, 0 WARNs
- **WARN**: 0 FAILs, 1+ WARNs
- **FAIL**: 1+ FAILs

## Container Restart Protocol

At `--restart-at` (default T+2h):

1. **Snapshot** pre-restart state from current tick: `budget.monthly_used`, `entities.total`
2. **Restart**: `docker restart njord-e2e`
3. **Poll** `/alive` every 2s (60s timeout)
4. **Log** recovery time in seconds
5. **Wait** 30s for HA status coordinator + stream reconnection
6. **Validate** post-restart state: budget preserved, entity count stable
7. **Resume** normal tick collection with phase `post_restart`

### Expected Restart Behavior

| Component | During restart | After recovery |
|-----------|---------------|----------------|
| `/alive` | Unreachable | 200 within ~15-30s |
| `/healthz` | Unreachable/503 | Healthy within ~30s |
| BudgetTracker | N/A | State restored from persistence |
| PollWorker | N/A | Timers fire immediately if overdue |
| GrpcConsumer | N/A | Empty cache, data after first poll |
| HA stream sensors | "off" | "on" within ~30-60s |
| HA status sensors | "unavailable" | Recover within ~30s |
| HA weather entities | Last known value | Update after first poll |

## Running the Test

### Manual Execution

```bash
# 1. Start stack
docker compose -f e2e/docker-compose.e2e.yml -f e2e/docker-compose.soak.yml up -d --build

# 2. Complete HA onboarding (see E2E-TEST-PLAN.md S1-S2)
#    Save the long-lived access token

# 3. Start collector
bash e2e/soak/soak-collector.sh --ha-token "<token>"

# 4. Wait 4 hours...

# 5. Review results
cat e2e/soak/soak-events.log
# Agent evaluation: invoke soak-test skill
```

### Agent Skill

```
/soak-test
```

The skill orchestrates setup, collection, and evaluation automatically.

### Shorter Test Run

```bash
bash e2e/soak/soak-collector.sh \
  --ha-token "<token>" \
  --duration 1h \
  --restart-at 30m
```

## Real-Cycle Profile (Production Intervals)

Tests all domains at their production polling intervals (not accelerated).
The longest cycle is Seasonal/Flood at 24h, so the test runs 48h for 2 full cycles.

### Configuration

No polling interval overrides -- `DomainDefaults` apply:

| Domain | Poll Interval | Cycles in 48h |
|--------|--------------|---------------|
| Forecast | 1h | ~48 |
| AirQuality | 1h | ~48 |
| Marine | 6h | ~8 |
| Ensemble | 6h | ~8 |
| Seasonal | 24h | ~2 |
| Flood | 24h | ~2 |

### Parameters

| Parameter | Value | Rationale |
|-----------|-------|-----------|
| Duration | 48h | 2 full Seasonal/Flood cycles |
| Collection interval | 15m | 192 ticks, enough granularity without noise |
| Restart at | 20h | Before the first 24h cycle completes, tests recovery mid-cycle |
| Compose override | `docker-compose.realcycle.yml` | Sets `PollInterval=01:00:00` (global floor), no domain overrides |

### Budget Estimate (48h, 1 location)

Empirical from the first 48h run: **~165 calls** (~3.4 calls/hour).

- Forecast: 48 cycles x ~3 call weight = ~144 calls
- AirQuality: 48 cycles x ~1 call weight = ~48 calls (overlaps with Forecast timing)
- Marine: 8 cycles x ~1 call = ~8 calls
- Ensemble: 8 cycles x ~4 calls = ~32 calls
- Seasonal: 2 cycles x 1 call = ~2 calls
- Flood: 2 cycles x ~1 call = ~2 calls
- **Total: ~165 calls (0.06% of 300k monthly)**

Note: call weights depend on the number of requested variables per domain,
which depends on which features are enabled. The earlier estimate of ~598
assumed higher per-cycle weights; actual weights are lower with fewer
variables per domain.

### Per-Domain Staleness Thresholds

The accelerated profile checks a single global staleness threshold. The real-cycle
profile differentiates by domain, since Seasonal/Flood entities are naturally "old"
between their 24h poll cycles.

| Domain | Threshold (min) | Rationale |
|--------|----------------|-----------|
| Forecast | 120 | 1h poll + 1h grace |
| AirQuality | 120 | 1h poll + 1h grace |
| Marine | 420 | 6h poll + 1h grace |
| Ensemble | 420 | 6h poll + 1h grace |
| Seasonal | 1500 | 24h poll + 1h grace |
| Flood | 1500 | 24h poll + 1h grace |

Entity classification uses entity_id patterns (see `soak-collector.sh`).

### Profile-Aware Checks

The main evaluation matrix (above) handles both profiles. Checks marked
**[profile-aware]** automatically adapt based on the JSONL data:
- `staleness_thresholds` present -> per-domain E3/E4 checks
- `grpc_status.phase` never reaches STEADY -> P1 checks for ACTIVE instead
- Longer poll intervals -> M3 allows proportionally more stalls

### Running

```bash
# 1. Start stack with real-cycle compose
docker compose -f e2e/docker-compose.e2e.yml -f e2e/docker-compose.realcycle.yml up -d --build

# 2. Complete HA onboarding (see E2E-TEST-PLAN.md S1-S2)

# 3. Start collector with realcycle profile
bash e2e/soak/soak-collector.sh --ha-token "<token>" --profile realcycle

# 4. Wait 48 hours...

# 5. Review results
cat e2e/soak/soak-events.log
```

### What This Test Catches (That Accelerated Doesn't)

- Discovery-to-Learned transition for slow domains (does Seasonal actually learn its ~24h cycle?)
- Entity staleness between long poll intervals (are HA entities correctly "available" with old data?)
- Budget accounting over many Forecast cycles vs few Seasonal cycles
- Recovery of a slow-domain timer mid-cycle (restart at 20h, Seasonal timer should fire at ~24h)
- Stream idle timeout for domains that don't produce data for hours
