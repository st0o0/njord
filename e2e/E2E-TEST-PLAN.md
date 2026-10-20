# E2E Test Plan — njord + ha-njord

Agent-orchestrated end-to-end test against a real Docker stack.
gRPC + REST. Real Open-Meteo API. 4 domains, 19 alert types, lifestyle enrichments.

## Section Index

| Section | Name | Steps | Agent |
|---------|------|-------|-------|
| S0 | Stack Setup | 4 | Opus |
| S1 | HA Onboarding + Token | 6 | Opus |
| S2 | Integration Setup | 4 | Opus |
| S3 | Entity Registration | 5 | Opus |
| S4 | Domain Data: Weather | 5 | Haiku #1 |
| S5 | Domain Data: Air Quality | 4 | Haiku #1 |
| S6 | Domain Data: Flood | 3 | Haiku #1 |
| S7 | Alert Binary Sensors | 5 | Haiku #2 |
| S8 | gRPC: GetDomainData + StreamDomainData | 5 | Haiku #2 |
| S9 | gRPC: GetEnrichments + StreamEnrichments | 4 | Haiku #2 |
| S10 | gRPC: GetCatalog + GetStatus + TriggerPoll | 5 | Haiku #3 |
| S11 | Server + Diagnostics Entities | 4 | Haiku #3 |
| S12 | Error Handling | 3 | Haiku #3 |
| S13 | Multi-Cycle: TriggerPoll × 2 | 4 | Opus |
| S14 | Budget Tracking | 3 | Opus |
| S15 | Teardown | 3 | Opus |
| | **Total** | **~67** | |

---

## Stack

| Service | Image | Ports |
|---------|-------|-------|
| njord | Built from Dockerfile | 8080 (HTTP), 8081 (gRPC) |
| homeassistant | HA stable | 8123 |

### Configuration

- **Location:** Lucerne (explicit coordinates 47.0502 / 8.3093 in the compose file)
- **Weather:** Ensemble Mean, ForecastDays=4, IncludeSolar=true
- **Enrichments:** ALL enabled (19 enrichment toggles)
- **Resolved domains:** Weather, AirQuality, Flood, Marine (4 calls/cycle)

### Expected Entities

Derived from config: 1 location, all enrichments enabled.

| Platform | Count | Pattern | Notes |
|----------|-------|---------|-------|
| weather | 1 | `weather.lucerne` | HA weather entity |
| sensor (AQ) | 7 | `lucerne_pm2_5`, `lucerne_pm10`, `lucerne_ozone`, `lucerne_birch_pollen`, `lucerne_grass_pollen`, `lucerne_alder_pollen`, `lucerne_mugwort_pollen` | Air quality |
| sensor (flood) | 1 | `lucerne_river_discharge` | River discharge m³/s |
| sensor (server) | 4 | `server_monthly_usage`, `server_daily_usage`, `server_version`, `server_uptime` | Diagnostics |
| sensor (target) | 1 | `server_lucerne_target` | Poll target timestamp |
| binary_sensor (alerts) | 19 | `lucerne_{frost,heat,...,swimming_conditions}_alert` | on when severity > none |
| binary_sensor (streams) | 2 | `server_domain_data_stream`, `server_enrichment_stream` | Connectivity |
| event | 1 | `lucerne_weather_alert` | Alert event entity |
| button | 1 | `server_trigger_poll` | Manual poll trigger |
| **Total** | **~37** | | |

---

## S0 — Stack Setup

1. `docker compose -f e2e/docker-compose.e2e.yml down -v`
2. `docker compose -f e2e/docker-compose.e2e.yml up -d --build`
3. Poll `http://localhost:8080/alive` → HTTP 200 (timeout 60s)
4. Poll `http://localhost:8123/` → HTTP 200 or 302 (timeout 120s)

---

## S1 — HA Onboarding + Token

Try claude-in-chrome first, API fallback if extension unavailable.

1. Check onboarding state: `GET /api/onboarding`
2. Create user: `POST /api/onboarding/users` → `auth_code`
3. Exchange for token: `POST /auth/token`
4. Complete remaining onboarding steps (analytics, core config)
5. Create long-lived access token via websocket `auth/long_lived_access_token`
6. Verify: `GET /api/` with `Authorization: Bearer <token>` → 200

---

## S2 — Integration Setup

1. `POST /api/config/config_entries/flow` with `{"handler":"njord"}`
2. `POST /api/config/config_entries/flow/<flow_id>` with `{"host":"njord","port":8081}`
3. Complete enrichment selection step (all enabled)
4. Record `entry_id` from response

---

## S3 — Entity Registration

1. Poll `/api/states` until weather entity appears (5s interval, 120s timeout)
2. Count entities matching `njord` platform: expect ~37
3. Verify `weather.lucerne` exists with state ∈ HA condition list
4. Verify at least 1 AQ sensor exists (pattern: `sensor.lucerne_pm*`)
5. Verify at least 1 alert binary_sensor exists (pattern: `binary_sensor.lucerne_*_alert`)

---

## Parallel Block — Spawn 3 Haiku Subagents

After S3 completes, spawn three subagents in a single message.

---

## S4 — Domain Data: Weather (Haiku #1)

1. `weather.lucerne` has `temperature` attribute (numeric)
2. `weather.lucerne` has `humidity` attribute (numeric, 0-100)
3. `weather.lucerne` has `wind_speed` attribute (numeric, ≥ 0)
4. `weather.lucerne` has `forecast` attribute (list, non-empty)
5. `weather.lucerne` state is a valid HA weather condition string

---

## S5 — Domain Data: Air Quality (Haiku #1)

1. `sensor.lucerne_pm2_5` has numeric state ≥ 0
2. `sensor.lucerne_ozone` has numeric state ≥ 0
3. At least one pollen sensor has numeric state (birch, grass, alder or mugwort)
4. AQ sensors have `unit_of_measurement` attribute

---

## S6 — Domain Data: Flood (Haiku #1)

1. `sensor.lucerne_river_discharge` has numeric state ≥ 0
2. `sensor.lucerne_river_discharge` has `unit_of_measurement` attribute
3. Discharge value is plausible (0–10000 m³/s)

---

## S7 — Alert Binary Sensors (Haiku #2)

1. At least 14 alert binary_sensors exist matching `*_alert`
2. Each has `state` ∈ {"on", "off"}
3. Each has `severity` attribute ∈ {"none", "yellow", "orange", "red"}
4. `frost_alert` and `heat_alert` exist (core weather alerts)
5. `pollen_alert` and `pm25_alert` exist (AQ alerts)

---

## S8 — gRPC: GetDomainData + StreamDomainData (Haiku #2)

Using `grpcurl` with `-import-path protos -proto njord/v2/weather.proto`:

1. `GetDomainData` with location "Lucerne" and domain `DOMAIN_WEATHER` (domain is required) → response has `weather_data` non-empty
2. `GetDomainData` with domain filter `DOMAIN_AIR_QUALITY` → response has `air_quality_data`
3. `StreamDomainData` → receives at least 1 update within 120s
4. Domain data update contains `location` field matching "Lucerne"
5. Weather data has `temperature`, `wind_speed`, `precipitation` fields

---

## S9 — gRPC: GetEnrichments + StreamEnrichments (Haiku #2)

1. `GetEnrichments` with location "Lucerne" → has `alerts` list
2. Alerts list has at least 1 entry with `type` and `severity`
3. `StreamEnrichments` → receives at least 1 event within 120s
4. Event has `type_name` = "alerts" with alert data

---

## S10 — gRPC: GetCatalog + GetStatus + TriggerPoll (Haiku #3)

1. `GetCatalog` → has at least 1 location named "Lucerne"
2. `GetStatus` → `budget` has `monthly_used` ≥ 0 and `monthly_limit` > 0
3. `GetStatus` → `activeEnrichments` lists every enabled enrichment; poll state has 1 target with a phase
4. `TriggerPoll` with location "Lucerne" → `triggeredCount` ≥ 1
5. After trigger: `GetStatus` shows incremented usage

---

## S11 — Server + Diagnostics Entities (Haiku #3)

1. `sensor.server_monthly_usage` has numeric state
2. `sensor.server_version` state has a non-empty state (`0.0.0-dev` in local builds)
3. `sensor.server_uptime` has numeric state (hours)
4. `button.server_trigger_poll` exists

---

## S12 — Error Handling (Haiku #3)

1. `GET /healthz` → 200 with healthy or degraded status
2. `GET /alive` → 200
3. Stream binary sensors (`server_domain_data_stream`, `server_enrichment_stream`) are "on"

---

## S13 — Multi-Cycle: TriggerPoll × 2 (Opus)

1. Record initial `monthly_used` from `GetStatus`
2. `TriggerPoll` location "Lucerne" → `triggeredCount` ≥ 1
3. Wait 30s for poll round to complete
4. Verify `monthly_used` increased (expect increment = resolved domain count)

---

## S14 — Budget Tracking (Opus)

1. `GetStatus` → `daily_used` > 0
2. `GetStatus` → `monthly_limit` = 300000 (free tier default)
3. Budget reflects actual API calls made during the test

---

## S15 — Teardown (Opus)

1. Remove integration: `DELETE /api/config/config_entries/entry/<entry_id>`
2. Verify njord entities removed from `/api/states`
3. `docker compose -f e2e/docker-compose.e2e.yml down -v`
