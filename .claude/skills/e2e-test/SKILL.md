---
name: e2e-test
description: Run the agent-orchestrated E2E test against a real Docker stack (njord + Home Assistant with ha-njord). Executes ~65 test steps across 14 sections with 3 parallel Haiku subagents. Covers gRPC API (4 RPCs), 6 domain data types, 30 alert types, 23 observations, entity validation, health endpoints, multi-cycle budget tracking, and teardown. Produces a PASS/FAIL results document.
---

# E2E Test — njord + ha-njord

Run the full end-to-end test suite against the real Docker stack.
~65 steps across 14 sections (S0-S15), 3 parallel Haiku subagents.

## Prerequisites

- Docker running
- ha-njord cloned at `../ha-njord` relative to this repo root
- Chrome browser available (for claude-in-chrome) — optional, API fallback for S1/S2
- grpcurl installed (for gRPC calls; use `-plaintext -import-path protos -proto njord/njord.proto`)

## Execution

Read the test plan at `e2e/E2E-TEST-PLAN.md` for the full step list.

### S0 — Stack Setup

```bash
cd <repo-root>
docker compose -f e2e/docker-compose.e2e.yml down -v
docker compose -f e2e/docker-compose.e2e.yml up -d --build
```

Poll for health:
- njord: `curl http://localhost:8080/alive` → HTTP 200 (timeout 60s)
- HA: `curl http://localhost:8123/` → HTTP 200 or 302 (timeout 120s)

### S1 — HA Setup (Browser preferred, API fallback)

Try claude-in-chrome first. If the extension is unavailable, fall back to the
HA REST/websocket API (see E2E-TEST-PLAN.md S1 for both paths).

### S2 — Integration Setup

Add njord integration via HA API:
1. Start config flow with `{"handler":"njord"}`
2. Complete with host=njord, port=8081
3. Record entry_id for teardown (no feature step: features are configured in njord)

### S3 — Entity Registration

Poll `/api/states` until `weather.barcelona` appears (timeout 120s).
Expect ~97 entities: 4 weather, ~23 observations, ~30 lifestyle sensors, 4 scores,
6 conditions, 30 alert binary_sensors, 4 server sensors, 1 button.

### Parallel Block — Spawn 3 Haiku Subagents

After S3 completes, spawn **three subagents in a single message** (parallel):

**Haiku #1 — Domain Data Validation (S4-S6):**
> Validate weather entity attributes, AQ sensor values, flood discharge data.
> HA API at http://localhost:8123 with token. Steps from E2E-TEST-PLAN.md S4-S6.

**Haiku #2 — Alerts + gRPC (S7-S9):**
> Validate alert binary_sensors, scores, conditions, GetManifest/GetStatus, Subscribe.
> HA API + grpcurl at localhost:8081. Steps from E2E-TEST-PLAN.md S7-S9.

**Haiku #3 — Ops + Server + Health (S10-S12):**
> Validate GetStatus/TriggerPoll, server entities, health endpoints.
> HA API + grpcurl at localhost:8081. Steps from E2E-TEST-PLAN.md S10-S12.

### After Parallel Block — Sequential (Opus)

**S13:** Multi-cycle test — TriggerPoll, wait, verify budget increment.
**S14:** Budget tracking — verify daily/monthly counts.
**S15:** Teardown — remove integration, verify entity cleanup, `docker compose down -v`.

## Results

Write results to `e2e/results/<timestamp>-e2e.md` with:
- PASS/FAIL per section
- Total pass/fail/skip counts
- Duration
- Any failures with details

## Notes

- **4 RPCs:** GetManifest, Subscribe, GetStatus, TriggerPoll (package `njord`, service `njord.NjordService`)
- **6 domains:** Forecast, Ensemble, AirQuality, Flood, Marine, Seasonal
- **87 features:** 64 regular + 23 observations, all enabled in E2E compose
- **Entity-first:** entities are defined by GetManifest, state delivered via Subscribe
- **Alerts may be "unavailable"** when the feature rule has not yet evaluated (domain not polled or returns null)
