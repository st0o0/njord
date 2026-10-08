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
- Chrome browser available (for claude-in-chrome)
- grpcurl installed (for gRPC calls)

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

### S1 — HA Setup (Browser)

Load claude-in-chrome tools first:
```
ToolSearch with query "select:mcp__claude-in-chrome__tabs_context_mcp,mcp__claude-in-chrome__navigate,mcp__claude-in-chrome__computer,mcp__claude-in-chrome__read_page,mcp__claude-in-chrome__tabs_create_mcp,mcp__claude-in-chrome__tabs_close_mcp,mcp__claude-in-chrome__form_input"
```

#### S1A — HA Onboarding (first run only)

1. Open `http://localhost:8123` in a new tab
2. HA shows onboarding wizard on first run
3. Create account: name "njord-e2e", username "njord", password "e2e-test-2026"
4. Complete onboarding: set location to Lucerne, timezone Europe/Zurich
5. Skip analytics, finish

If HA shows the dashboard instead of onboarding, skip S1A (already set up from a
previous run that didn't `down -v`).

#### S1B — Create Long-Lived Access Token

1. Click user menu (bottom-left) → Profile
2. Scroll to "Long-Lived Access Tokens"
3. Click "Create Token", name: "e2e-test"
4. **Copy the token value immediately** — it is shown only once
5. Store it in a variable for all subsequent REST API calls

All REST API calls use header: `Authorization: Bearer <token>`

### S2 — Integration Setup (Browser)

1. Navigate to Settings → Devices & Services
2. Click "Add Integration"
3. Search "njord"
4. Enter host: `njord`, port: `8081`
5. Submit — should show success with location/model counts

Record time from submit to success confirmation.

### S3 — Entity Registration

Poll HA REST API until entities appear:

```bash
curl -s -H "Authorization: Bearer <token>" http://localhost:8123/api/states | jq '[.[] | select(.entity_id | startswith("weather."))] | length'
```

Wait until weather entities appear (poll every 5s, timeout 120s).

Then verify:
- 3 weather entities (lucerne_icon_d2, lucerne_ecmwf_ifs025, lucerne_consensus)
- ~47 total njord entities (±5 tolerance)
- Record total entity count and full entity ID list

### Parallel Block A — Spawn 3 Haiku Subagents

After S3 completes, spawn **three subagents in a single message** (so they
run in parallel):

**Haiku #1 — Weather + Entity Depth (S4–S7):**
Brief it with:
- HA URL: `http://localhost:8123`
- Auth token
- S4 steps (GetCatalog + Streaming RPCs)
- S5 steps (GetForecast)
- S6 steps (GetEnrichments)
- S7 steps (entity attribute depth + enrichment validation — 22 steps)
- Expected entities: 3 weather, 14 alerts, 11 indices, 5 derived, 1 trend, 1 history
- Use grpcurl for gRPC, curl for HA REST API
- Report PASS/FAIL per step with detail

**Haiku #2 — Connectivity + Ops + Errors (S8–S11):**
Brief it with:
- HA URL: `http://localhost:8123`
- Auth token
- S8 steps (connectivity entities — 4 binary_sensors)
- S9 steps (server entities — version, uptime, usage, targets)
- S10 steps (OpsService RPCs — GetStatus, GetTargets)
- S11 steps (error handling — invalid gRPC + REST requests)
- Use grpcurl for gRPC, curl for HA REST API
- Report PASS/FAIL per step with detail

**Haiku #3 — HA Browser Verification (S12–S14):**
Brief it with:
- HA URL: `http://localhost:8123`
- Auth token (for browser login if needed)
- S12 steps (weather entity cards in Developer Tools)
- S13 steps (enrichment entities in Developer Tools)
- S14 steps (server entities in Developer Tools)
- Use claude-in-chrome tools (load them first via ToolSearch)
- Report PASS/FAIL per step with detail

Use `Agent` tool with `model: "haiku"` for all three. Wait for all to complete.

### FAIL Verification

When subagents return, check each FAIL:
- Re-run the failing API call yourself
- Distinguish Haiku misinterpretation from real bug
- Mark as PASS if the re-check succeeds, keep as FAIL if confirmed

### S15 — SensorService (Sequential)

Push sensor readings via grpcurl:

```bash
grpcurl -plaintext -d '{"kind":"SENSOR_KIND_INDOOR_TEMPERATURE","location":"lucerne","source":"e2e-test","value":21.5}' localhost:8081 njord.v2.SensorService/Push
```

Verify `accepted` = true. Test rejection with unknown location.

### S16–S17 — Multi-Cycle + Budget

1. Record `last_updated` for `weather.lucerne_icon_d2`
2. `grpcurl -plaintext -d '{}' localhost:8081 njord.v2.OpsService/TriggerPoll`
3. Poll until `last_updated` advances (5s interval, 120s timeout)
4. Repeat for second cycle
5. Read `sensor.daily_usage` before and after — verify increment
6. Compare gRPC `GetStatus` budget fields with HA sensor values

### S18 — AdminService: GetConfig

```bash
grpcurl -plaintext localhost:8081 njord.v2.AdminService/GetConfig
```

Verify config matches Docker environment (locations, models, horizons, enrichments).

### S19–S21 — Config Mutation: Alerts Cycle

**Disable alerts (S19):**
```bash
grpcurl -plaintext -d '{"alerts":{"enabled":false}}' localhost:8081 njord.v2.AdminService/SetEnrichment
grpcurl -plaintext -d '{}' localhost:8081 njord.v2.OpsService/TriggerPoll
```

**Verify removal (S20):**
Poll until 14 alert sensors + 1 event entity become unavailable/disappear (timeout 60s).
Verify entity count dropped by ~15.

**Re-enable (S21):**
```bash
grpcurl -plaintext -d '{"alerts":{"enabled":true}}' localhost:8081 njord.v2.AdminService/SetEnrichment
grpcurl -plaintext -d '{}' localhost:8081 njord.v2.OpsService/TriggerPoll
```

Poll until all 14 alert sensors reappear (timeout 120s). Verify count restored.

### S22 — Config Mutation: Horizons

```bash
grpcurl -plaintext -d '{"horizons":[6,24]}' localhost:8081 njord.v2.AdminService/SetSettings
grpcurl -plaintext -d '{}' localhost:8081 njord.v2.OpsService/TriggerPoll
```

Wait 30s, verify forecast reflects changed horizons.

**Restore:**
```bash
grpcurl -plaintext -d '{"horizons":[3,6,12,24,48,72]}' localhost:8081 njord.v2.AdminService/SetSettings
```

### S23 — Resilience: Container Restart

```bash
docker stop njord-e2e
```

1. Wait 10s, verify stream sensors show "off" and weather entities show "unavailable"
2. `docker start njord-e2e`
3. Poll `/alive` until 200 (timeout 60s)
4. Poll `binary_sensor.forecast_stream` until "on" (timeout 120s)
5. Verify weather entities recovered

### S24 — Teardown: Integration Removal (Browser)

1. Navigate to Settings → Devices & Services
2. Find njord integration, delete it
3. Poll `GET /api/states` until no njord entities remain (timeout 30s)
4. Verify 0 orphaned njord entities
5. `docker compose -f e2e/docker-compose.e2e.yml down -v`

Clean up the browser tab.

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
