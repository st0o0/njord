---
name: e2e-test
description: Run the agent-orchestrated E2E test against a real Docker stack (njord + Home Assistant with ha-njord). Executes ~81 test steps across 16 sections with 3 parallel Haiku subagents. Covers gRPC API (8 RPCs), 6 domain data types, 22 alert types, entity validation, error handling, multi-cycle budget tracking, and teardown. Produces a PASS/FAIL results document.
---

# E2E Test — njord + ha-njord

Run the full end-to-end test suite against the real Docker stack.
~81 steps across 16 sections (S0–S15), 3 parallel Haiku subagents.

## Prerequisites

- Docker running
- ha-njord cloned at `../ha-njord` relative to this repo root
- Chrome browser available (for claude-in-chrome) — optional, API fallback for S1/S2
- grpcurl installed (for gRPC calls; use `-import-path protos -proto njord/v2/<service>.proto`)

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

Poll `/api/states` until `weather.lucerne` appears (timeout 120s).
Expect ~45 entities: 2 weather, 7 AQ sensors, 1 flood sensor, ~7 lifestyle sensors,
22 alert binary_sensors, 2 lifestyle binary_sensors, 4 server sensors, 2 stream sensors, 1 event, 1 button.

### Parallel Block — Spawn 3 Haiku Subagents

After S3 completes, spawn **three subagents in a single message** (parallel):

**Haiku #1 — Domain Data Validation (S4-S6):**
> Validate weather entity attributes, AQ sensor values, flood discharge data.
> HA API at http://localhost:8123 with token. Steps from E2E-TEST-PLAN.md S4-S6.

**Haiku #2 — Alerts + gRPC Streaming (S7-S9):**
> Validate alert binary_sensors, GetDomainData/StreamDomainData, GetEnrichments/StreamEnrichments.
> HA API + grpcurl at localhost:8081. Steps from E2E-TEST-PLAN.md S7-S9.

**Haiku #3 — Ops + Server + Errors (S10-S12):**
> Validate GetCatalog/GetStatus/TriggerPoll, server entities, health endpoints.
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

## Key Differences from Previous Plan

- **No AdminService** — config is static, no mutation RPCs
- **No SensorService** — no indoor sensor push
- **Model comparison** — one weather entity per location plus one per secondary model (ModelComparison)
- **No consensus/derived/trends/indices** — replaced by alert + lifestyle enrichments
- **6 domains** (Weather, AQ, Flood, Marine, Seasonal, Satellite)
- **22 alert types** instead of 14
- **Feature-driven config** — feature toggles derive polled domains
- **Explicit coordinates** — every location carries latitude and longitude
- **~81 steps** instead of ~148 (simpler, more focused)
