---
name: e2e-test
description: Run the agent-orchestrated E2E test against a real Docker stack (njord + Home Assistant with ha-njord). Executes the test plan, manages Docker lifecycle, browser automation, and parallel subagents. Produces a PASS/FAIL results document.
---

# E2E Test — njord + ha-njord

Run the full end-to-end test suite against the real Docker stack.

## Prerequisites

- Docker running
- ha-njord cloned at `../ha-njord` relative to this repo root
- Chrome browser available (for claude-in-chrome)

## Execution

Read the test plan at `e2e/E2E-TEST-PLAN.md` for the full step list. This skill
orchestrates its execution.

### Phase 0 — Stack Setup

```bash
cd <repo-root>
docker compose -f e2e/docker-compose.e2e.yml down -v
docker compose -f e2e/docker-compose.e2e.yml up -d --build
```

Poll for health:
- njord: `curl http://localhost:8080/alive` → HTTP 200 (timeout 60s)
- HA: `curl http://localhost:8123/` → HTTP 200 or 302 (timeout 120s)

Record the startup time.

### Phase 1 — HA Setup (Browser)

Load claude-in-chrome tools first:
```
ToolSearch with query "select:mcp__claude-in-chrome__tabs_context_mcp,mcp__claude-in-chrome__navigate,mcp__claude-in-chrome__computer,mcp__claude-in-chrome__read_page,mcp__claude-in-chrome__tabs_create_mcp,mcp__claude-in-chrome__tabs_close_mcp,mcp__claude-in-chrome__form_input"
```

#### 1A — HA Onboarding (first run only)

1. Open `http://localhost:8123` in a new tab
2. HA shows onboarding wizard on first run
3. Create account: name "njord-e2e", username "njord", password "e2e-test-2026"
4. Complete onboarding: set location to Lucerne, timezone Europe/Zurich
5. Skip analytics, finish

If HA shows the dashboard instead of onboarding, skip 1A (already set up from a
previous run that didn't `down -v`).

#### 1B — Create Long-Lived Access Token

1. Click user menu (bottom-left) → Profile
2. Scroll to "Long-Lived Access Tokens"
3. Click "Create Token", name: "e2e-test"
4. **Copy the token value immediately** — it is shown only once
5. Store it in a variable for all subsequent REST API calls

All REST API calls use header: `Authorization: Bearer <token>`

#### 1C — Add njord Integration

1. Navigate to Settings → Devices & Services
2. Click "Add Integration"
3. Search "njord"
4. Enter host: `njord`, port: `8081`
5. Submit — should show success with location/model counts

Record time from submit to success confirmation.

### Phase 2 — Entity Registration

Poll HA REST API until entities appear:

```bash
curl -s -H "Authorization: Bearer <token>" http://localhost:8123/api/states | jq '[.[] | select(.entity_id | startswith("weather."))] | length'
```

Wait until weather entities appear (poll every 5s, timeout 120s).

Then verify:
- 3 weather entities (lucerne_icon_d2, lucerne_ecmwf_ifs025, lucerne_consensus)
- Record total entity count and full entity ID list

### Parallel Phase — Spawn Haiku Subagents

After Phase 2 completes, spawn **two subagents in a single message** (so they
run in parallel):

**Haiku #1 — Forecast + Enrichment (Phase 3 + 4):**
Brief it with:
- HA URL: `http://localhost:8123`
- Auth token
- Phase 3 steps (forecast data) + Phase 4 steps (enrichment entities)
- Report PASS/FAIL per step with detail

**Haiku #2 — Connectivity + gRPC (Phase 5 + 6):**
Brief it with:
- HA URL: `http://localhost:8123`
- Auth token
- Phase 5 steps (connectivity/server entities) + Phase 6 steps (direct gRPC)
- Report PASS/FAIL per step with detail

Use `Agent` tool with `model: "haiku"` for both. Wait for both to complete.

### FAIL Verification

When subagents return, check each FAIL:
- Re-run the failing API call yourself
- Distinguish Haiku misinterpretation from real bug
- Mark as PASS if the re-check succeeds, keep as FAIL if confirmed

### Phase 7 — Resilience

```bash
docker restart njord-e2e
```

1. Poll `http://localhost:8080/alive` until 200 (timeout 60s)
2. Poll `GET /api/states/binary_sensor.forecast_stream` until `on` (timeout 120s)
3. Check `weather.lucerne_icon_d2` state ≠ "unavailable"
4. Record reconnect duration

### Phase 8 — Trigger Poll

Either via browser (press button.trigger_poll in HA UI) or via REST API:

```bash
curl -s -X POST -H "Authorization: Bearer <token>" \
  -H "Content-Type: application/json" \
  -d '{"entity_id": "button.trigger_poll"}' \
  http://localhost:8123/api/services/button/press
```

Wait 30s, verify `weather.lucerne_icon_d2` `last_updated` is recent.

### Write Results

Create `e2e/results/E2E-TEST-RESULTS-<YYYY-MM-DD>.md` with:

1. **Summary table** — Phase / Step / Result (PASS/FAIL) / Detail
2. **Timing baselines** — startup, integration→entities, reconnect
3. **Entity list** — all entity IDs observed
4. **Notes** — any observations, warnings, or issues found

Rules:
- Every step gets PASS or FAIL — **no SKIP**
- FAIL includes the reason
- Timing baselines are recorded for future comparison

### Teardown

After results are written:

```bash
docker compose -f e2e/docker-compose.e2e.yml down -v
```

Clean up the browser tab.
