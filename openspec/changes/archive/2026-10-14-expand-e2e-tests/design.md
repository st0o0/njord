## Context

The current E2E test plan (`e2e/E2E-TEST-PLAN.md`) covers 44 steps across 8 sequential
phases with 2 parallel Haiku subagents. It proves the stack boots, entities register,
and basic gRPC calls return data — but leaves most of the gRPC surface untested (2 of 16
RPCs), never validates entity attribute correctness, and never exercises error paths,
config mutations, or multi-cycle behaviour.

The FunkArr E2E plan (~170+ steps, 29 sections, 3 parallel subagents) is the reference
for coverage depth, state management via setup recipes, and parallel verification.

The njord gRPC surface spans 4 services / 16 RPCs:

| Service | RPCs |
|---------|------|
| WeatherService | GetCatalog, GetForecast, GetEnrichments, StreamForecasts, StreamEnrichments |
| OpsService | GetStatus, GetTargets, TriggerPoll |
| AdminService | GetConfig, StreamConfig, SetLocations, SetSettings, SetEnrichment, SetBudget |
| SensorService | Push, StreamPush |

## Goals / Non-Goals

**Goals:**

- Expand from 44 to ~150+ steps across ~25 sections
- Test every gRPC RPC (16 total), including streaming and error responses
- Validate entity attributes (values, units, device_class, state_class) not just existence
- Verify enrichment computed values fall within expected ranges
- Test error handling (invalid gRPC requests, bad entity IDs)
- Exercise multi-poll-cycle behaviour and config hot-reload via AdminService
- Verify HA UI displays entity cards correctly (browser)
- Verify clean teardown (integration removal → entities disappear)
- Expand to 3 parallel Haiku subagents

**Non-Goals:**

- Mocking the Open-Meteo API (tests run against the real API)
- Testing HA core features beyond the njord integration
- Performance benchmarking or load testing
- MQTT-path verification (gRPC path via ha-njord only)

## Decisions

### 1. Section ordering: progressive state build-up

**Decision:** Sections execute in a fixed order that builds state progressively. Each
section's preconditions are satisfied by earlier sections completing.

**Dependency graph (simplified):**

```
S0  Stack Setup
S1  HA Onboarding + Token         (depends: S0)
S2  Integration Setup             (depends: S1)
S3  Entity Registration           (depends: S2)
    ┌──────────────────────────────┼──────────────────────────────┐
    │ PARALLEL BLOCK A             │                              │
    │                              │                              │
S4  │ WeatherService: GetCatalog   S8  Connectivity Entities      S12 HA Browser: Weather Cards
S5  │ WeatherService: GetForecast  S9  Server Entities            S13 HA Browser: Enrichment Cards
S6  │ WeatherService: GetEnrichm.  S10 OpsService: Full           S14 HA Browser: Server Entities
S7  │ Entity Attribute Depth       S11 gRPC Error Handling
    └──────────────────────────────┼──────────────────────────────┘
S15 SensorService: Push + StreamPush                (depends: parallel block A)
S16 Multi-Cycle: TriggerPoll × 2                    (depends: S15)
S17 Budget Tracking Across Cycles                   (depends: S16)
S18 AdminService: GetConfig + StreamConfig          (depends: S17)
S19 Config Mutation: SetEnrichment (disable alerts)  (depends: S18)
S20 Config Mutation: Verify entity removal           (depends: S19)
S21 Config Mutation: Re-enable alerts                (depends: S20)
S22 Config Mutation: SetSettings (change horizons)   (depends: S21)
S23 Resilience: Container Restart                    (depends: S22)
S24 Teardown: Integration Removal                    (depends: S23)
```

**Why over alternatives:** A single ordered run (vs multiple isolated runs per scenario)
avoids repeated stack-up/onboarding costs (~2 min each). AdminService mutations
mid-run change config without restarting, and the test verifies the system adapts.

### 2. Three-subagent parallelisation

**Decision:** After entity registration (S3), spawn 3 Haiku subagents in a single
message for parallel block A:

| Subagent | Sections | Focus |
|----------|----------|-------|
| Haiku #1 — Weather + Entities | S4–S7 | WeatherService RPCs, entity attribute depth |
| Haiku #2 — Connectivity + Ops | S8–S11 | Server/connectivity entities, OpsService RPCs, gRPC errors |
| Haiku #3 — HA Browser | S12–S14 | Browser verification of entity cards in HA UI |

**Why 3 not 2:** The browser subagent (claude-in-chrome) is slow — element location,
screenshots, page loading. Running it in parallel with the API subagents avoids it
becoming a bottleneck. This matches FunkArr's 3-agent pattern.

**Why not more:** Beyond 3, subagent coordination overhead outweighs parallelism gains.
The sequential sections (S15–S24) must run in order and are handled by the Opus main
agent.

### 3. Config mutation via AdminService (not Docker overrides)

**Decision:** Test configuration scenarios via AdminService gRPC mutations
(SetEnrichment, SetSettings, SetLocations, SetBudget) rather than multiple
docker-compose override files or full stack restarts.

**What gets mutated during the run:**
1. **S19:** `SetEnrichment` to disable alerts → verify 14 alert sensors disappear from HA
2. **S21:** `SetEnrichment` to re-enable alerts → verify they reappear
3. **S22:** `SetSettings` to change horizons from [3,6,12,24,48,72] to [6,24] → verify
   forecast entries reflect the change

**Why over Docker overrides:** The AdminService exists precisely for runtime config
changes. Testing it validates a real user workflow (ha-njord config panel → gRPC
mutation → entity set update). Docker overrides would only test startup-time config,
which the existing plan already covers.

**Why not SetLocations:** Adding/removing a location mid-run is the most disruptive
mutation (new model targets, new enrichment devices). Reserve it for a future "advanced
scenarios" expansion — the current run validates the mutation mechanism via enrichment
and settings changes.

### 4. Error handling: invalid requests via grpcurl

**Decision:** Test error responses by sending malformed or invalid gRPC requests
directly via grpcurl and verifying the error codes and messages.

**Error scenarios:**
- `GetForecast` with unknown location → expect gRPC NOT_FOUND or INVALID_ARGUMENT
- `GetForecast` with unknown model → expect gRPC NOT_FOUND or INVALID_ARGUMENT
- `GetEnrichments` with empty location → expect gRPC INVALID_ARGUMENT
- `SensorService.Push` with `SENSOR_KIND_UNSPECIFIED` → expect rejection
- `SensorService.Push` with unknown location → expect rejection
- `SetBudget` with 0 requests → expect rejection or warning
- HA REST API: `GET /api/states/sensor.nonexistent_entity` → expect 404 or empty

**Why not config-level errors:** Invalid config (e.g. bad model names) is validated at
startup and tested in unit tests. E2E focuses on runtime error paths that cross the
gRPC boundary.

### 5. Multi-cycle testing: TriggerPoll + timestamp comparison

**Decision:** Exercise multi-poll-cycle behaviour by:
1. Record `last_updated` timestamp for `weather.lucerne_icon_d2` (from HA REST API)
2. Call `OpsService.TriggerPoll` via grpcurl
3. Poll until `last_updated` advances (timeout 120s, poll every 5s)
4. Record the new timestamp, verify it's newer
5. Repeat for a second cycle
6. Verify budget counters incremented (`OpsService.GetStatus` → `budget.daily_used`)

**Why TriggerPoll not wait:** The default poll interval is 60 min. Waiting for natural
cycles would make the E2E run >2 hours. TriggerPoll is the mechanism ha-njord uses for
the "Trigger Poll" button.

### 6. HA browser verification: entity cards

**Decision:** Use claude-in-chrome to verify entities display correctly in the HA UI,
not just via REST API:

- Navigate to a weather entity's detail page → verify temperature, humidity, wind values
  are displayed and numeric
- Navigate to an alert sensor → verify state (none/warning/critical) is displayed
- Navigate to server entities → verify version string, uptime, usage values display

**Why browser:** The HA REST API proves the backend state is correct. Browser
verification proves the ha-njord custom component correctly translates gRPC data to HA
entities that render in the UI. This catches ha-njord frontend bugs that API tests
miss.

**What not to test in browser:** Lovelace card customisation, dashboard editing, HA
automation triggers — these are HA core features, not njord integration.

### 7. Teardown: integration removal via HA UI

**Decision:** Remove the njord integration via the HA Settings UI (browser) and verify:
1. All njord entities disappear from `GET /api/states` (poll with timeout)
2. The integration no longer appears in Settings → Devices & Services
3. gRPC streams from ha-njord to njord are closed (check njord logs or `GetStatus`)

**Why browser removal not API:** HA integration removal is a UI-driven workflow. The
HA REST API has no direct "remove integration" endpoint — it goes through the config
flow system. Testing via browser validates the real user path.

## Risks / Trade-offs

**[Open-Meteo API unavailability] → Mitigation:** The E2E depends on live API responses
for forecast data. If the API is down, weather entity states remain "unavailable" and
data validation steps fail. Mitigation: the test plan documents which steps are
API-dependent so failures can be triaged quickly. The error handling sections (S11) do
not depend on API data.

**[HA UI selector fragility] → Mitigation:** Browser automation relies on CSS selectors
or element text that can change between HA releases. Mitigation: pin the HA image digest
in `docker-compose.e2e.yml` (already done: `sha256:3e6710a...`). Selector updates are a
maintenance cost on HA version bumps, not a design flaw.

**[Config mutation leaving inconsistent state] → Mitigation:** Sections S19–S22 mutate
config and verify the system adapts. If a mutation leaves the system in an inconsistent
state, subsequent sections (resilience, teardown) will fail with cascading FAILs.
Mitigation: each mutation section has explicit rollback verification (re-enable after
disable, restore horizons). The resilience section (S23) acts as a final consistency
check.

**[Longer test runtime] → Mitigation:** Estimated 15–20 min (up from ~5 min) due to
multi-cycle waits, config mutation settle times, and browser verification. Acceptable
for a manual, infrequent test run. The 3-agent parallelism in block A offsets some of
the growth.

**[Streaming RPC verification complexity] → Mitigation:** StreamForecasts and
StreamEnrichments are server-streaming RPCs. grpcurl can consume them but blocks until
the stream ends or a timeout. Mitigation: use `timeout 10 grpcurl ...` to bound the
wait, verify at least one message was received, then kill the stream. StreamConfig is
similarly bounded.

## Open Questions

- **Sensor data injection:** SensorService.Push requires a known location and valid
  SensorKind. Should the E2E push a mock indoor temperature reading and verify it
  influences enrichment indices (night ventilation, VPD)? This would validate the full
  sensor → enrichment path but adds complexity. Recommend: yes, include in S15 as a
  simple push + verify flow.
- **SetLocations scope:** Should a future expansion test adding a second location
  mid-run (e.g. Zurich) and verify the entity count doubles? Deferred for now per
  decision #3.
