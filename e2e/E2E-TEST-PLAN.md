# E2E Test Plan — njord + ha-njord

Agent-orchestrated end-to-end test against a real Docker stack.
gRPC path only (no MQTT). Real Open-Meteo API.

## Stack

| Service | Image | Ports | Notes |
|---------|-------|-------|-------|
| njord | Built from repo Dockerfile | 8080 (HTTP), 8081 (gRPC) | 1 location, 2 models, all enrichments |
| homeassistant | ghcr.io/home-assistant/home-assistant:stable | 8123 | ha-njord mounted as custom_component |

### Configuration

- **Location:** lucerne (47.05, 8.31)
- **Models:** icon_d2, ecmwf_ifs025
- **Enrichments:** consensus, alerts, derived, trends, indices, history (all enabled)
- **Horizons:** +3, +6, +12, +24, +48, +72 h

### Expected Entities (~47)

Derived from config: 1 location × 2 models × all enrichments.

| Platform | Count | Pattern |
|----------|-------|---------|
| weather | 3 | `lucerne_icon_d2`, `lucerne_ecmwf_ifs_0_25deg`, `lucerne_consensus` |
| sensor (alerts) | 14 | `lucerne_{frost,heat,storm,heavy_rain,uv,fog,snow,pressure_drop,thunderstorm,ice,wind_chill,visibility,tropical_night,humidity}_alert` |
| sensor (indices) | 11 | `lucerne_{laundry,outdoor,running,cycling,bbq,irrigation,solar,night_ventilation}_index`, `lucerne_vpd`, `lucerne_frost_hours`, `lucerne_frost_confidence` |
| sensor (trends) | 1 | `lucerne_weather_trend` |
| sensor (derived) | 5 | `lucerne_{sunshine,diurnal_amplitude,beaufort,wind_chill,dewpoint_comfort}` |
| sensor (history) | 1 | `lucerne_model_performance` |
| sensor (server) | 4 | `server_monthly_usage`, `server_daily_usage`, `server_version`, `server_uptime` |
| sensor (targets) | 2 | `lucerne_icon_d2_target`, `lucerne_ecmwf_ifs_0_25deg_target` |
| binary_sensor | 4 | `lucerne_inversion`, `server_forecast_stream`, `server_enrichment_stream`, `server_config_stream` |
| event | 1 | `lucerne_weather_alert` |
| button | 1 | `server_trigger_poll` |

---

## Phase 0 — Stack Setup

**Agent:** Opus (main)

| Step | Action | Pass Criteria |
|------|--------|---------------|
| 0.1 | `docker compose -f e2e/docker-compose.e2e.yml down -v` | No errors |
| 0.2 | `docker compose -f e2e/docker-compose.e2e.yml up -d --build` | Both containers start |
| 0.3 | Poll `http://localhost:8080/alive` until HTTP 200 (timeout: 60s) | njord healthy |
| 0.4 | Poll `http://localhost:8123/` until HTTP 200 or 302 (timeout: 120s) | HA healthy |
| 0.5 | Record startup duration (time from 0.2 to 0.4 complete) | Documented |

---

## Phase 1 — HA Integration Setup (Browser)

**Agent:** Opus (main), using claude-in-chrome

### 1A — HA Onboarding (first run only)

Fresh HA instances require initial setup before integrations can be added.

| Step | Action | Pass Criteria |
|------|--------|---------------|
| 1A.1 | Open `http://localhost:8123` in browser | Onboarding page loads |
| 1A.2 | Create user account (name: "njord-e2e", username: "njord", password: "e2e-test-2026") | Account created |
| 1A.3 | Complete onboarding wizard (location, timezone, telemetry opt-out) | Dashboard appears |

### 1B — Create Long-Lived Access Token

| Step | Action | Pass Criteria |
|------|--------|---------------|
| 1B.1 | Navigate to user profile (click user icon → profile) | Profile page loads |
| 1B.2 | Scroll to "Long-Lived Access Tokens" section | Section visible |
| 1B.3 | Click "Create Token", name: "e2e-test" | Token displayed |
| 1B.4 | Copy token value, store for REST API calls | Token captured |

### 1C — Add njord Integration

| Step | Action | Pass Criteria |
|------|--------|---------------|
| 1C.1 | Navigate to Settings → Devices & Services | Integrations page loads |
| 1C.2 | Click "Add Integration" | Integration search dialog opens |
| 1C.3 | Search for "njord" | njord integration appears |
| 1C.4 | Select njord, enter host: `njord`, port: `8081` | Config flow form visible |
| 1C.5 | Submit | Success message with location/model counts |
| 1C.6 | Record time from submit to success | Documented |

---

## Phase 2 — Entity Registration

**Agent:** Opus (main), using HA REST API

| Step | Action | Pass Criteria |
|------|--------|---------------|
| 2.1 | Poll `GET /api/states` with auth token (interval: 5s, timeout: 120s) until weather entities appear | At least 1 `weather.*` entity exists |
| 2.2 | Count total entities with `njord` in device identifiers | ~47 entities (±5 tolerance for first run) |
| 2.3 | Verify 3 weather entities exist: `weather.lucerne_icon_d2`, `weather.lucerne_ecmwf_ifs025`, `weather.lucerne_consensus` | All 3 present |
| 2.4 | Record time from integration setup (1C.5) to first entities | Documented |
| 2.5 | Record complete entity ID list | Documented for future reference |

---

## Phase 3 — Forecast Data Validation

**Agent:** Haiku subagent #1 (parallel)

Auth: use long-lived access token from Phase 1B.

| Step | Action | Pass Criteria |
|------|--------|---------------|
| 3.1 | `GET /api/states/weather.lucerne_icon_d2` | State ≠ "unavailable", `attributes.temperature` is numeric |
| 3.2 | `GET /api/states/weather.lucerne_ecmwf_ifs025` | State ≠ "unavailable", `attributes.temperature` is numeric |
| 3.3 | `POST /api/services/weather/get_forecasts` with `entity_id: weather.lucerne_icon_d2, type: hourly` | Response contains forecast entries |
| 3.4 | Verify forecast entries have `temperature`, `humidity`, `wind_speed` keys | All keys present |
| 3.5 | `GET /api/states/weather.lucerne_consensus` | State ≠ "unavailable" |
| 3.6 | Verify consensus attributes include `agreement` or `spread` or `reliable_hours` | At least one present |

---

## Phase 4 — Enrichment Entity Validation

**Agent:** Haiku subagent #1 (continues from Phase 3)

| Step | Action | Pass Criteria |
|------|--------|---------------|
| 4.1 | Verify 14 alert sensors exist (`sensor.lucerne_*_alert`) | All 14 present |
| 4.2 | Pick one alert sensor, verify attributes include `severity` and `confidence` | Attributes present |
| 4.3 | Verify 11 index sensors exist (8 `*_index` + `vpd` + `frost_hours` + `frost_confidence`) | All 11 present |
| 4.4 | Verify `sensor.lucerne_weather_trend` exists | Present |
| 4.5 | Verify 5 derived sensors exist (`sunshine`, `diurnal_amplitude`, `beaufort`, `wind_chill`, `dewpoint_comfort`) | All 5 present |
| 4.6 | Verify `sensor.lucerne_model_performance` exists | Present |
| 4.7 | Verify `binary_sensor.lucerne_inversion` exists | Present |
| 4.8 | Verify `event.lucerne_weather_alert` exists | Present |

---

## Phase 5 — Connectivity & Server Entities

**Agent:** Haiku subagent #2 (parallel)

Auth: use long-lived access token from Phase 1B.

| Step | Action | Pass Criteria |
|------|--------|---------------|
| 5.1 | `GET /api/states/binary_sensor.forecast_stream` | State = "on" |
| 5.2 | `GET /api/states/binary_sensor.enrichment_stream` | State = "on" |
| 5.3 | `GET /api/states/binary_sensor.config_stream` | State = "on" |
| 5.4 | `GET /api/states/sensor.version` | State is non-empty string |
| 5.5 | `GET /api/states/sensor.uptime` | State is non-empty |
| 5.6 | `GET /api/states/sensor.monthly_usage` | State is numeric |
| 5.7 | `GET /api/states/sensor.daily_usage` | State is numeric |
| 5.8 | Verify `button.trigger_poll` exists | Present |
| 5.9 | Verify 2 target sensors exist: `sensor.lucerne_icon_d2_target`, `sensor.lucerne_ecmwf_ifs025_target` | Both present |

---

## Phase 6 — Direct gRPC Validation

**Agent:** Haiku subagent #2 (continues from Phase 5)

| Step | Action | Pass Criteria |
|------|--------|---------------|
| 6.1 | `grpcurl -plaintext localhost:8081 njord.v2.OpsService/GetStatus` | Response contains `version` field |
| 6.2 | Verify response contains locations array with "lucerne" | Present |
| 6.3 | Verify response contains models matching config (icon_d2, ecmwf_ifs025) | Both present |
| 6.4 | `grpcurl -plaintext localhost:8081 njord.v2.OpsService/GetBudgetStatus` | Response contains usage fields |

---

## Phase 7 — Resilience

**Agent:** Opus (main)

| Step | Action | Pass Criteria |
|------|--------|---------------|
| 7.1 | `docker restart njord-e2e` | Container restarts |
| 7.2 | Poll `http://localhost:8080/alive` until HTTP 200 (timeout: 60s) | njord healthy again |
| 7.3 | Poll `GET /api/states/binary_sensor.forecast_stream` until state = "on" (timeout: 120s) | gRPC stream reconnected |
| 7.4 | Verify `weather.lucerne_icon_d2` state ≠ "unavailable" | Entity still functional |
| 7.5 | Record reconnect duration (time from 7.1 to 7.3 complete) | Documented |

---

## Phase 8 — Trigger Poll (Browser)

**Agent:** Opus (main), using claude-in-chrome

| Step | Action | Pass Criteria |
|------|--------|---------------|
| 8.1 | Navigate to HA developer tools or entity page for `button.trigger_poll` | Entity visible |
| 8.2 | Press the trigger_poll button (or call `POST /api/services/button/press` with entity_id) | Service call succeeds |
| 8.3 | Wait 30s, then check `weather.lucerne_icon_d2` `last_updated` | Timestamp is recent (within last 60s) |

---

## Orchestration

```
┌────────────────────────────────────────────────────┐
│  Opus Main Agent                                    │
│  Phase 0: Docker up, health poll              [SEQ] │
│  Phase 1: Browser — onboarding, token, config [SEQ] │
│  Phase 2: Entity registration polling         [SEQ] │
│                                                     │
│  ── Parallel after Phase 2 ───────────────────────  │
│  ┌─────────────────┐     ┌─────────────────┐       │
│  │  Haiku #1       │     │  Haiku #2       │       │
│  │  Phase 3: Data  │     │  Phase 5: Conn  │       │
│  │  Phase 4: Enrich│     │  Phase 6: gRPC  │       │
│  └─────────────────┘     └─────────────────┘       │
│                                                     │
│  Phase 7: Resilience (restart/reconnect)      [SEQ] │
│  Phase 8: Trigger poll (browser or API)       [SEQ] │
│                                                     │
│  ── Finalize ─────────────────────────────────────  │
│  Re-validate any Haiku FAILs                        │
│  Write results document                             │
└────────────────────────────────────────────────────┘
```

### Subagent Briefing

Each Haiku subagent receives:
- The HA base URL (`http://localhost:8123`)
- The long-lived access token
- Their assigned phases from this plan
- Instruction to report PASS/FAIL per step with detail

The main agent re-checks any FAIL before accepting it (Haiku may misinterpret API responses).

---

## Results Format

Output: `e2e/results/E2E-TEST-RESULTS-<YYYY-MM-DD>.md`

### Summary Table

| Phase | Step | Result | Detail |
|-------|------|--------|--------|
| 0 | 0.1 Clean shutdown | PASS | No errors |
| 0 | 0.2 Stack start | PASS | Both containers running |
| ... | ... | ... | ... |

Rules:
- Every step gets PASS or FAIL — **no SKIP allowed**
- FAIL includes the reason (error message, unexpected value, timeout)
- Timing observations are recorded in the Detail column

### Timing Baselines

Recorded on first run, used as reference for future runs:

| Metric | First Run | Notes |
|--------|-----------|-------|
| Stack startup (0.2 → 0.4) | 9s | njord instant, HA ~5s |
| Integration setup to entities (1C.5 → 2.1) | <0.1s | Entities register on config flow completion |
| Config flow duration | 0.23s | gRPC GetCatalog fast |
| Restart → njord healthy | 5.7s | |
| Restart → stream reconnect (7.1 → 7.3) | 11.8s | forecast_stream off→on |
