---
name: e2e-test
description: Run the agent-orchestrated E2E test against a real Docker stack (njord + Home Assistant with ha-njord). Executes ~151 test steps across 25 sections with 3 parallel Haiku subagents. Covers gRPC API (all 16 RPCs), entity attribute validation, enrichment computation, error handling, multi-poll-cycle, config hot-reload, HA browser verification, and teardown. Produces a PASS/FAIL results document.
---

# E2E Test — njord + ha-njord

Run the full end-to-end test suite against the real Docker stack.
~151 steps across 25 sections (S0–S24), 3 parallel Haiku subagents.

## Prerequisites

- Docker running
- ha-njord cloned at `../ha-njord` relative to this repo root
- Chrome browser available (for claude-in-chrome) — optional, API fallback for S1/S2/S24
- grpcurl installed (for gRPC calls; reflection is disabled, use `-import-path protos -proto njord/v2/<service>.proto`)

## Execution

Read the test plan at `e2e/E2E-TEST-PLAN.md` for the full step list. This skill
orchestrates its execution.

### S0 — Stack Setup

```bash
cd <repo-root>
docker compose -f e2e/docker-compose.e2e.yml down -v
docker compose -f e2e/docker-compose.e2e.yml up -d --build
```

Poll for health:
- njord: `curl http://localhost:8080/alive` → HTTP 200 (timeout 60s)
- HA: `curl http://localhost:8123/` → HTTP 200 or 302 (timeout 120s)

Record the startup time.

### S1 — HA Setup (Browser preferred, API fallback)

Try claude-in-chrome first. If the extension is unavailable, fall back to the
HA REST/websocket API (see E2E-TEST-PLAN.md S1 for both paths).

**Browser path:** Load chrome tools via ToolSearch, open `http://localhost:8123`,
complete onboarding wizard, create long-lived access token in Profile.

**API fallback:**
1. `GET /api/onboarding` → if all steps `done: false`, onboarding needed
2. `POST /api/onboarding/users` → creates user, returns `auth_code`
3. Exchange auth_code via `POST /auth/token`, complete remaining onboarding steps
4. Create long-lived token via websocket `auth/long_lived_access_token`

All REST API calls use header: `Authorization: Bearer <token>`

### S2 — Integration Setup (Browser preferred, API fallback)

**Browser path:** Settings → Devices & Services → Add Integration → njord → host `njord`, port `8081`.

**API fallback:**
1. `POST /api/config/config_entries/flow` with `{"handler":"njord"}`
2. `POST /api/config/config_entries/flow/<flow_id>` with `{"host":"njord","port":8081}`
3. Record `entry_id` from response (needed for S24)

### S3 — Entity Registration + Enable All

Poll HA REST API until weather entities appear (5s interval, 120s timeout).

**S3A — Default entity set (out-of-the-box experience):**
- Verify ~26 entities in `/api/states` (enabled-by-default: 3 weather, 14 alerts, 4 server sensors, 3 stream binary_sensors, 1 event, 1 button)
- Verify 3 weather entities: `weather.lucerne_icon_d2`, `weather.lucerne_ecmwf_ifs_0_25deg`, `weather.lucerne_consensus`

**S3B — Enable all disabled entities (for subsequent tests):**
- Query entity registry via websocket `config/entity_registry/list`, filter `platform: "njord"`
- Enable all entities with `disabled_by: "integration"` (21 entities: indices, derived, trend, history, inversion, targets)
- Reload integration, verify ~47 total entities in `/api/states`

### Parallel Block A — Spawn 3 Haiku Subagents

After S3 completes, spawn **three subagents in a single message** (so they
run in parallel):

**Haiku #1 — Weather + Entity Depth (S4–S7):**
Brief it with:
- HA URL: `http://localhost:8123`
- Auth token (long-lived, not short-lived JWT)
- S4 steps (GetCatalog + Streaming RPCs)
- S5 steps (GetForecast)
- S6 steps (GetEnrichments)
- S7 steps (entity attribute depth + enrichment validation — 22 steps)
- Expected entities: 3 weather, 14 alerts, 11 indices, 5 derived, 1 trend, 1 history
- **Critical:** grpcurl needs `-import-path protos -proto njord/v2/weather.proto` (no reflection)
- Key attribute names: consensus uses `available_models` (not `models_used`); indices are 0–100 (not 0–10); alert `trigger_value`/`threshold` only present when severity ≠ "none"
- Use PowerShell for REST API calls (no `jq` in Git Bash)
- Report PASS/FAIL per step with detail

**Haiku #2 — Connectivity + Ops + Errors (S8–S11):**
Brief it with:
- HA URL: `http://localhost:8123`
- Auth token (long-lived)
- S8 steps (connectivity entities — entity IDs use `server_` prefix: `binary_sensor.server_forecast_stream` etc.)
- S9 steps (server entities — `sensor.server_version`, `sensor.server_uptime`, targets: `sensor.server_icon_d2_lucerne`)
- S10 steps (OpsService RPCs — GetStatus, GetTargets)
- S11 steps (error handling — invalid gRPC + REST requests)
- **Critical:** grpcurl needs `-import-path protos -proto njord/v2/<service>.proto`
- Use PowerShell for REST API calls
- Report PASS/FAIL per step with detail

**Haiku #3 — HA Verification (S12–S14):**
Brief it with:
- HA URL: `http://localhost:8123`
- Auth token (long-lived)
- S12 steps (weather entities — consensus attribute: `available_models`)
- S13 steps (enrichment entities — use active alert for attribute check, indices 0–100)
- S14 steps (server entities — `sensor.server_version`, `sensor.server_daily_usage`)
- Use claude-in-chrome (preferred) or HA REST API (fallback)
- Report PASS/FAIL per step with detail

Use `Agent` tool with `model: "haiku"` for all three. Wait for all to complete.

### FAIL Verification

When subagents return, check each FAIL:
- Re-run the failing API call yourself
- Distinguish Haiku misinterpretation from real bug
- Mark as PASS if the re-check succeeds, keep as FAIL if confirmed

### S15 — SensorService (Sequential)

Push sensor readings via grpcurl (all commands need `-import-path protos -proto njord/v2/sensor.proto`):

```bash
grpcurl -plaintext -import-path protos -proto njord/v2/sensor.proto -d '{"kind":"SENSOR_KIND_INDOOR_TEMPERATURE","location":"lucerne","source":"e2e-test","value":21.5}' localhost:8081 njord.v2.SensorService/Push
```

Verify `accepted` = true. Test rejection with unknown location.

### S16–S17 — Multi-Cycle + Budget

Track cycles via `budget.dailyUsed` from gRPC GetStatus (HA `last_updated` is
unreliable — only advances when the state string actually changes).

1. Record `budget.dailyUsed` via `grpcurl ... njord.v2.OpsService/GetStatus`
2. `grpcurl ... njord.v2.OpsService/TriggerPoll`
3. Wait 15–20s, read `budget.dailyUsed` again — verify increment
4. Repeat for second cycle
5. Read `sensor.server_daily_usage` before and after — verify HA sensor tracks it
6. Verify gRPC `dailyUsed` is consistent with HA sensor (HA shows % of daily budget)

### S18 — AdminService: GetConfig

```bash
grpcurl -plaintext -import-path protos -proto njord/v2/admin.proto localhost:8081 njord.v2.AdminService/GetConfig
```

Verify config matches Docker environment (locations, models, horizons, enrichments).
Note: horizons may be duplicated due to env + default merge.

### S19–S21 — Config Mutation: Alerts Cycle

**Disable alerts (S19):**
```bash
grpcurl -plaintext -import-path protos -proto njord/v2/admin.proto -d '{"alerts":{"enabled":false}}' localhost:8081 njord.v2.AdminService/SetEnrichment
grpcurl -plaintext -import-path protos -proto njord/v2/ops.proto -d '{}' localhost:8081 njord.v2.OpsService/TriggerPoll
```

**Verify removal (S20):**
Poll until 14 alert sensors + 1 event entity become unavailable (timeout 60s).
Verify entity count dropped by ~15.

**Re-enable (S21):**
```bash
grpcurl -plaintext -import-path protos -proto njord/v2/admin.proto -d '{"alerts":{"enabled":true}}' localhost:8081 njord.v2.AdminService/SetEnrichment
grpcurl -plaintext -import-path protos -proto njord/v2/ops.proto -d '{}' localhost:8081 njord.v2.OpsService/TriggerPoll
```

Poll until all 14 alert sensors reappear (timeout 120s). Verify count restored.

### S22 — Config Mutation: Horizons

```bash
grpcurl -plaintext -import-path protos -proto njord/v2/admin.proto -d '{"horizons":[6,24]}' localhost:8081 njord.v2.AdminService/SetSettings
grpcurl -plaintext -import-path protos -proto njord/v2/ops.proto -d '{}' localhost:8081 njord.v2.OpsService/TriggerPoll
```

Wait 15–20s, verify GetConfig shows changed horizons.

**Restore:**
```bash
grpcurl -plaintext -import-path protos -proto njord/v2/admin.proto -d '{"horizons":[3,6,12,24,48,72]}' localhost:8081 njord.v2.AdminService/SetSettings
```

### S23 — Resilience: Container Restart

```bash
docker stop njord-e2e
```

1. Wait 10s, verify `binary_sensor.server_forecast_stream` = "off"
2. `docker start njord-e2e`
3. Poll `/alive` until 200 (timeout 60s)
4. Poll `binary_sensor.server_forecast_stream` until "on" (timeout 120s)
5. Verify weather entities recovered (state ≠ "unavailable")

### S24 — Teardown (Browser preferred, API fallback)

**Browser path:** Settings → Devices & Services → njord → Delete.

**API fallback:** `DELETE /api/config/config_entries/entry/<entry_id>`

Then:
1. Poll `GET /api/states` until no njord entities remain (timeout 30s)
2. Verify 0 orphaned njord entities
3. `docker compose -f e2e/docker-compose.e2e.yml down -v`

### Write Results

Create `e2e/results/E2E-TEST-RESULTS-<YYYY-MM-DD>.md` with:

1. **Summary table** — Section / Step / Result (PASS/FAIL) / Detail
   - ~151 rows covering all sections S0–S24
2. **Timing baselines** — startup, integration→entities, poll cycle, config settle,
   reconnect, teardown, full run duration
3. **Entity list** — all entity IDs observed
4. **Config scenario results** — entity count before/after disable, horizon change effects
5. **Notes** — any observations, warnings, or issues found

Rules:
- Every step gets PASS or FAIL — **no SKIP**
- FAIL includes the reason
- Timing baselines are recorded for future comparison
- Config mutation results document the entity count delta
