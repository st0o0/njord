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
      - Njord__Polling__DiscoveryInterval=00:05:00
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

### Expected Numbers (4h, PollInterval=15m, 1 location, all 65 features)

- Poll cycles: ~16
- Collector ticks: ~48 (at 5-min intervals)
- API calls: ~176 (16 cycles x 11 call weight)
- Budget impact: negligible (0.06% of 300k monthly)

## Phases

| Phase | Timeframe | Trigger | What happens |
|-------|-----------|---------|-------------|
| `warmup` | T+0 to ~T+5m | Start | Stack is up, waiting for first poll |
| `discovery` | ~T+5m to ~T+15m | `fetch_total > 0` | Domains learning their update cycle |
| `steady_state` | ~T+15m to T+2h | GetStatus phase = `POLL_PHASE_STEADY` | Regular polling, all domains learned |
| `pre_restart` | T+2h | `elapsed >= restart_at` | Snapshot state, trigger restart |
| `restarting` | T+2h to T+2h+~30s | `docker restart` issued | Container down, /alive polled |
| `recovery` | T+2h+~30s to T+2h+~60s | `/alive` responds | Waiting for HA reconnection |
| `post_restart` | T+2h+~60s to T+4h | Post-restart validation done | Same as steady_state, verifying recovery |
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
    "phase": "POLL_PHASE_DISCOVERY",
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

### Health

| ID | Check | Condition | Severity | Restart |
|----|-------|-----------|----------|---------|
| H1 | Never Unhealthy | `health.status != "Unhealthy"` in all ticks | FAIL | excl. restart |
| H2 | Degraded rare | `health.status == "Degraded"` in <= 2 ticks | WARN | excl. restart |

### Entity Stability

| ID | Check | Condition | Severity | Restart |
|----|-------|-----------|----------|---------|
| E1 | Entity count constant | `entities.total` same across all ticks | FAIL | all ticks |
| E2 | Zero unavailable | `entities.unavailable_count == 0` in all ticks | FAIL | excl. restart |
| E3 | No stale entities | `entities.stalest_age_min < 30` in all ticks | FAIL | excl. restart |
| E4 | Freshness warning | `entities.stalest_age_min < 20` in all ticks | WARN | excl. restart |

### Budget

| ID | Check | Condition | Severity | Restart |
|----|-------|-----------|----------|---------|
| B1 | Monotonic increase | `budget.monthly_used` never decreases between ticks | FAIL | all ticks |
| B2 | Final within range | Final `budget.monthly_used` within 15% of `cycles x 11` | WARN | end only |

### Polling Lifecycle

| ID | Check | Condition | Severity | Restart |
|----|-------|-----------|----------|---------|
| P1 | STEADY reached | `grpc_status.phase == "POLL_PHASE_STEADY"` within 30 min | FAIL | warmup only |
| P2 | Low miss count | `grpc_status.miss_count <= 2` in steady state | WARN | excl. restart |
| P3 | All enrichments | `grpc_status.enrichment_count == 65` in all ticks | FAIL | excl. restart |

### Streams and Metrics

| ID | Check | Condition | Severity | Restart |
|----|-------|-----------|----------|---------|
| S1 | Streams connected | `entities.stream_domain == "on"` and `stream_enrichment == "on"` | FAIL | excl. restart |
| M1 | No fetch failures | `metrics.fetch_total` only increases (no `pipeline_failed_total` growth) | WARN | all ticks |
| M2 | No pipeline failures | `metrics.pipeline_failed_total == 0` across all ticks | WARN | all ticks |
| M3 | Pipeline active | `metrics.pipeline_events_total` increases between non-restart ticks | WARN | excl. restart |

### Recovery

| ID | Check | Condition | Severity | Restart |
|----|-------|-----------|----------|---------|
| R1 | Recovery time | `/alive` within 60s of restart (from event log) | FAIL | restart only |
| R2 | Budget preserved | `budget.monthly_used` identical pre/post restart | FAIL | restart only |
| R3 | Entities re-registered | `entities.total` same post-restart within 120s | FAIL | restart only |
| R4 | Streams reconnected | Stream sensors "on" within 90s of restart | WARN | restart only |
| R5 | Fresh data within cycle | `pipeline_events_total` grows within one poll interval post-restart | WARN | restart only |

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
