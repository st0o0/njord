## 1. Compose Override

- [x] 1.1 Create `e2e/docker-compose.soak.yml` with `Njord__Polling__PollInterval=00:15:00` and `Njord__Polling__DiscoveryInterval=00:05:00` as environment overrides for the `njord` service. Verify it composes with `docker compose -f e2e/docker-compose.e2e.yml -f e2e/docker-compose.soak.yml config`.

## 2. Collector Script

- [x] 2.1 Create `e2e/soak/soak-collector.sh` with argument parsing: `--duration` (default `4h`), `--interval` (default `5m`), `--restart-at` (default `2h`), `--ha-token` (required), `--njord-url` (default `http://localhost:8080`), `--grpc-host` (default `localhost:8081`), `--ha-url` (default `http://localhost:8123`), `--output-dir` (default `e2e/soak`). Parse duration strings (`Nh`, `Nm`) to seconds. Validate `--ha-token` is provided. Set `#!/usr/bin/env bash` and `set -euo pipefail`.

- [x] 2.2 Implement the health collection function: `curl -sf "$NJORD_URL/healthz"` capturing both HTTP status code (`-w "%{http_code}"`) and body text. Return JSON fragment `{"status":"Healthy","http_code":200,"alive":true}`. Handle unreachable endpoint gracefully (return `{"status":"unreachable","http_code":0,"alive":false}`). Alive check: `curl -sf "$NJORD_URL/alive"`.

- [x] 2.3 Implement the Prometheus metrics collection function: `curl -sf "$NJORD_URL/metrics"`, parse with `grep` + `awk` to extract: `njord_openmeteo_fetch_total`, `njord_pipeline_domain_polls_total`, `njord_pipeline_cycle_learned_total`, `njord_pipeline_events_total`, `njord_pipeline_handler_skipped_total`, `njord_pipeline_producer_failed_total`, `njord_pipeline_data_changed_total`, `njord_budget_used_monthly`, `njord_budget_used_daily`, `njord_budget_limit_monthly`, `njord_budget_limit_daily`. Sum counter values across label combinations (e.g., `fetch_total` has domain/result tags). Output JSON fragment for `metrics` and `budget` sections.

- [x] 2.4 Implement the gRPC status collection function: `grpcurl -plaintext -import-path protos -proto njord/v2/ops.proto $GRPC_HOST njord.v2.OpsService/GetStatus`. Parse the JSON response to extract: `uptimeSeconds`, `budget.monthlyUsed`, `budget.dailyUsed`, `models[0].phase`, `models[0].missCount`, `models[0].cycleSeconds`, `activeEnrichments` count, `budgetPartial`, `modelsPartial`. Handle grpcurl failure gracefully (return `{"reachable":false}`).

- [x] 2.5 Implement the HA entity collection function: `curl -sf -H "Authorization: Bearer $HA_TOKEN" "$HA_URL/api/states"`. Parse with `grep`/`awk`/`sed` (no jq -- wait, jq IS a dependency) to extract: total entity count matching njord, unavailable entity count, `weather.barcelona` state and temperature attribute, stream binary sensor states (`server_domain_data_stream`, `server_enrichment_stream`), alert binary sensor on/off counts, stalest entity `last_updated` age in minutes. Handle unreachable HA gracefully.

- [x] 2.6 Implement phase detection logic. Track current phase as a variable, transition based on: `warmup` -> `discovery` (when `fetch_total` > 0), `discovery` -> `steady_state` (when gRPC phase = `POLL_PHASE_STEADY`), `steady_state` -> `pre_restart` (when `elapsed >= restart_at`), then restart sequence, then `post_restart` until end. Log MILE events on transitions to `soak-events.log`.

- [x] 2.7 Implement the container restart sequence: pre-restart state snapshot (budget_monthly_used, entity_count from current tick), `docker restart njord-e2e`, poll `/alive` every 2 seconds with 60-second timeout, log recovery time, wait 30 seconds for HA reconnection, post-restart validation tick. Log RESTART events to `soak-events.log`. Set phase to `restarting` during restart, `recovery` after `/alive` responds, `post_restart` after validation tick.

- [x] 2.8 Implement the main tick loop: calculate total ticks from `duration / interval`, loop with `sleep $interval_seconds` between ticks, call all four collection functions, assemble the full JSONL object with `tick`, `ts` (from `date -u +%Y-%m-%dT%H:%M:%SZ`), `elapsed_min`, `phase`, and the four collected sections, append to `soak-results.jsonl`. Write periodic TICK entries to `soak-events.log`. At the final tick, set phase to `complete` and log END event.

- [x] 2.9 Add `.gitignore` entries for runtime output: `e2e/soak/soak-results.jsonl` and `e2e/soak/soak-events.log`.

## 3. Soak Test Plan Document

- [x] 3.1 Create `e2e/soak/SOAK-TEST-PLAN.md` with sections: Purpose, Prerequisites (Docker, curl, jq, grpcurl), Configuration (compose override, default parameters), Phases (warmup/discovery/steady_state/restart/post_restart with expected timing), JSONL Schema (full field reference), Pass/Fail Matrix (all 19 checks with ID, description, condition, severity, restart-window exclusion), Container Restart Protocol (step-by-step with expected behavior timeline), and Running the Test (commands for manual execution and agent skill invocation).

## 4. Agent Skill

- [x] 4.1 Create `.claude/skills/soak-test/SKILL.md` with skill frontmatter (name, description). Document the three-phase orchestration: Phase 1 (Setup): compose up with soak override, HA onboarding reusing E2E S0-S2 flow, pass token to collector. Phase 2 (Collection): start `soak-collector.sh` in background, tell user to re-invoke when done. Phase 3 (Evaluation): read `soak-results.jsonl` line by line, apply all 19 pass/fail checks, generate structured report with overall PASS/WARN/FAIL, per-check results with evidence, restart recovery summary, and tick count/duration summary.

- [x] 4.2 Document the evaluation logic in the skill: for each of the 19 checks, specify the JSONL fields to inspect, the condition to test, how to identify the restart window (phase field), and the evidence to cite on failure. Include the expected budget calculation: `cycles x 11 call_weight` where cycles = `duration / poll_interval`.

## Validation

No production code changes -- no `dotnet run --project` test commands needed.

Verify compose override:
```bash
docker compose -f e2e/docker-compose.e2e.yml -f e2e/docker-compose.soak.yml config
```

Verify collector script syntax:
```bash
bash -n e2e/soak/soak-collector.sh
```
