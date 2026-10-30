## Purpose

The soak-test collector is a Python script that samples njord's health, metrics, gRPC status and HA entities over a multi-hour run, producing structured JSONL output for automated evaluation.

## Requirements

### Requirement: Periodic data collection

The collector script SHALL sample four data sources every 5 minutes for the configured duration (default 4 hours): HTTP `/healthz`, HTTP `/metrics` (Prometheus), gRPC `GetStatus`, and HA REST `/api/states`.

#### Scenario: Normal tick collection

- **WHEN** a tick fires during normal operation
- **THEN** the collector appends one JSON line to `soak-results.jsonl` containing: health status, Prometheus counter/gauge values, gRPC status fields (phase, miss_count, cycle_seconds, enrichment_count, uptime), and HA entity summary (total count, unavailable count, weather state, stream sensor states, alert on/off counts, stalest entity age)

#### Scenario: Data source unreachable during tick

- **WHEN** any of the four data sources is unreachable during a tick
- **THEN** the collector logs the error in the `errors` array of that tick's JSONL entry and continues to the next source (partial ticks are valid)

### Requirement: Structured JSONL output

Each tick SHALL produce one JSON object per line in `soak-results.jsonl` with fields: `tick` (sequential integer), `ts` (ISO 8601 UTC), `elapsed_min` (minutes since start), `phase` (one of: warmup, discovery, steady_state, pre_restart, restarting, recovery, post_restart, complete), `health`, `metrics`, `budget`, `grpc_status`, `entities`, `errors`.

#### Scenario: Output file integrity

- **WHEN** the collector completes or is interrupted
- **THEN** every line in `soak-results.jsonl` SHALL be valid JSON parseable independently

### Requirement: Human-readable event log

The collector SHALL write milestone events and anomalies to `soak-events.log` with format `<ISO timestamp> [<LEVEL>] <message>` where LEVEL is one of START, TICK, MILE, WARN, FAIL, RESTART, END.

#### Scenario: Milestone logging

- **WHEN** the first poll completes (fetch_total transitions from 0), the Discovery-to-Learned transition occurs (phase changes to STEADY), or the test completes
- **THEN** the collector writes a MILE entry to `soak-events.log`

### Requirement: Container restart orchestration

At the configured restart time (default T+2h), the collector SHALL: snapshot pre-restart state (budget monthly_used, entity count), execute `docker restart njord-e2e`, poll `/alive` with a 60-second timeout, wait 30 seconds for HA reconnection, then snapshot post-restart state.

#### Scenario: Successful restart and recovery

- **WHEN** the container restart completes and `/alive` responds within 60 seconds
- **THEN** the collector logs recovery time in seconds and records pre/post restart state in `soak-events.log`

#### Scenario: Recovery timeout

- **WHEN** `/alive` does not respond within 60 seconds after restart
- **THEN** the collector logs a FAIL event and continues collecting (does not abort the test)

### Requirement: Restart tolerance window

For 2 minutes after a container restart, the collector SHALL mark ticks with phase `restarting` or `recovery`. Evaluation criteria that exclude the restart window SHALL use this phase to filter ticks.

#### Scenario: Tick during restart

- **WHEN** a tick fires while njord is restarting (within 2 minutes of restart)
- **THEN** unreachable endpoints are logged but not counted as failures in the tick's `errors` array (the phase field distinguishes them)

### Requirement: Phase detection

The collector SHALL track the current phase based on elapsed time and observed state transitions: `warmup` (start until first poll detected), `discovery` (first poll until STEADY phase in GetStatus), `steady_state` (STEADY detected until restart time), `pre_restart`/`restarting`/`recovery` (restart sequence), `post_restart` (after recovery until end), `complete` (final tick).

#### Scenario: Automatic phase transition

- **WHEN** GetStatus reports `POLL_PHASE_STEADY` for the first time
- **THEN** the collector transitions from `discovery` to `steady_state` and logs a MILE event

### Requirement: Prometheus metric parsing

The collector SHALL parse the Prometheus text exposition format from `/metrics` to extract counter totals and histogram quantiles. At minimum: `njord_openmeteo_fetch_total`, `njord_pipeline_domain_polls_total`, `njord_pipeline_cycle_learned_total`, `njord_pipeline_events_total`, `njord_pipeline_handler_skipped_total`, `njord_pipeline_producer_failed_total`, `njord_budget_used_monthly`, `njord_budget_used_daily`, `njord_budget_limit_monthly`, `njord_budget_limit_daily`.

#### Scenario: Counter extraction

- **WHEN** `/metrics` returns Prometheus text format
- **THEN** the collector extracts the numeric value of each named metric and includes it in the tick's `metrics` and `budget` sections

### Requirement: Configurable parameters

The collector SHALL accept command-line arguments for: `--duration` (default 4h), `--interval` (default 5m), `--restart-at` (default 2h), `--ha-token` (required), `--njord-url` (default `http://localhost:8080`), `--grpc-host` (default `localhost:8081`), `--ha-url` (default `http://localhost:8123`), `--output-dir` (default `e2e/soak`).

#### Scenario: Custom duration

- **WHEN** the collector is started with `--duration 2h --restart-at 1h`
- **THEN** the restart occurs at T+1h and the collector runs for 2h total
