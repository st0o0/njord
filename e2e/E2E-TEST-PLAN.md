# E2E Test Plan — njord + ha-njord

Agent-orchestrated end-to-end test against a real Docker stack.
gRPC + REST. Real Open-Meteo API. 6 domains (Weather, AirQuality, Flood, Marine, Seasonal, Satellite),
20 alert types, lifestyle features, multi-model weather comparison.

## Section Index

| Section | Name | Steps | Agent |
|---------|------|-------|-------|
| S0 | Stack Setup | 4 | Opus |
| S1 | HA Onboarding + Token | 6 | Opus |
| S2 | Integration Setup | 3 | Opus |
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
| | **Total** | **~66** | |

---

## Stack

| Service | Image | Ports |
|---------|-------|-------|
| njord | Built from Dockerfile | 8080 (HTTP), 8081 (gRPC) |
| homeassistant | HA stable | 8123 |

### Configuration

- **Location:** Lucerne (explicit coordinates 47.0502 / 8.3093 in the compose file)
- **Weather:** ForecastDays=4, models `ecmwf_ifs025_ensemble_mean` (primary) + `ecmwf_aifs025_ensemble_mean`
  (`Weather:ModelComparison` enabled)
- **Features:** ALL enabled (37 feature toggles, `Njord__Features__*`)
- **Resolved domains:** Weather, AirQuality, Flood, Marine, Seasonal, Satellite (Marine fails for the inland
  location and backs off up to the poll interval)

### Expected Entities

Derived from config: 1 location, all features enabled.

| Platform | Count | Pattern | Notes |
|----------|-------|---------|-------|
| weather | 2 | `weather.lucerne`, one per secondary model | HA weather entities (primary + comparison model) |
| sensor (AQ) | 7 | `lucerne_pm2_5`, `lucerne_pm10`, `lucerne_ozone`, `lucerne_birch_pollen`, `lucerne_grass_pollen`, `lucerne_alder_pollen`, `lucerne_mugwort_pollen` | Air quality |
| sensor (flood) | 1 | `lucerne_river_discharge` | River discharge m³/s |
| sensor (lifestyle) | ~7 | `lucerne_commute_safety`, `lucerne_exercise`, `lucerne_model_temperature_spread`, `lucerne_seasonal_temperature_anomaly`, `lucerne_solar_yield`, ... | Varies by data availability; Marine-dependent sensors absent for inland |
| sensor (server) | 4 | `server_monthly_usage`, `server_daily_usage`, `server_version`, `server_uptime` | Diagnostics |
| binary_sensor (alerts) | 20 | `lucerne_{frost,heat,...}` alert binary sensors | on when severity > none |
| binary_sensor (lifestyle) | 2 | `lucerne_planting_safe`, `lucerne_ventilation_window` | Condition binary sensors |
| binary_sensor (streams) | 2 | `server_domain_data_stream`, `server_enrichment_stream` | Connectivity |
| event | 1 | `lucerne_weather_alert` | Alert event entity |
| button | 1 | `server_trigger_poll` | Manual poll trigger |
| **Total** | **~45** | | Entity ids follow HA device + name naming; unique ids are the contract. Count varies by data availability (Marine fails for inland). |

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
3. Record `entry_id` from response (no feature step: features are configured in njord)

---

## S3 — Entity Registration

1. Poll `/api/states` until weather entity appears (5s interval, 120s timeout)
2. Count entities matching `njord` platform: expect ~45
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
4. `POST /api/services/weather/get_forecasts?return_response` with `{"entity_id":"weather.lucerne","type":"hourly"}` → `service_response["weather.lucerne"].forecast` is a non-empty list (daily is unsupported)
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

1. 20 alert binary_sensors exist matching `*_alert`
2. Each has `state` ∈ {"on", "off"}
3. At least one alert from each category exists: frost, heat, storm, uv, pollen
4. `event.lucerne_weather_alert` exists with `event_types` including alert lifecycle events
5. Alert severity is delivered via the event entity data (not as a binary_sensor attribute)

---

## S8 — gRPC: GetDomainData + StreamDomainData (Haiku #2)

Using `grpcurl` with `-import-path protos -proto njord/v2/weather.proto`:

1. `GetDomainData` with location "Lucerne" and domain `DOMAIN_WEATHER` (domain is required) → response has the oneof `weather` non-empty
2. `GetDomainData` with domain filter `DOMAIN_AIR_QUALITY` → response has the oneof `airQuality`
3. Open `StreamDomainData` (no TriggerPoll needed) — the stream replays cached domain data immediately via `Source.From(snapshot).Concat(liveHub)`. Expect data within seconds.
4. Domain data update contains `location` field matching "Lucerne"
5. Weather data has `temperature`, `wind_speed`, `precipitation` fields (`precipitation` may be omitted when 0)
6. Note: Marine returns NotFound for inland locations by design

---

## S9 — gRPC: GetEnrichments + StreamEnrichments (Haiku #2)

1. `GetEnrichments` with location "Lucerne" → has an `events[]` list
2. `events[]` has at least 1 entry; the feature is the oneof case (e.g. `fog`) and the alert severity is at `<case>.alert.severity`
3. Open `StreamEnrichments` (no TriggerPoll needed) — the stream replays cached enrichment events immediately. Expect data within seconds.
4. Event has a feature oneof case set (`type_name` no longer exists)

---

## S10 — gRPC: GetCatalog + GetStatus + TriggerPoll (Haiku #3)

1. `GetCatalog` on `njord.v2.WeatherService` → has at least 1 location named "Lucerne" and lists weather models
2. `GetStatus` → `budget` has `monthly_used` ≥ 0 and `monthly_limit` > 0
3. `GetStatus` → `activeEnrichments` count ≥ 37 (all enabled features); poll state is reported per location
4. `TriggerPoll` → `triggeredCount` ≥ 1
5. After trigger: `GetStatus` shows incremented `monthlyUsed`

---

## S11 — Server + Diagnostics Entities (Haiku #3)

1. `sensor.server_monthly_usage` has numeric state (a percentage, `suggested_display_precision=1`; rounds to 0.0 early in the test since 23/300000 = 0.008%)
2. `sensor.server_version` state has a non-empty state (`0.0.0-dev` in local builds)
3. `sensor.server_uptime` has numeric state (hours, `suggested_display_precision=1`; rounds to 0.0 for the first ~6 minutes)
4. `button.server_trigger_poll` exists

---

## S12 — Error Handling (Haiku #3)

1. `GET /healthz` → 200 with plain text `Healthy` or `Degraded`
2. `GET /alive` → 200
3. Stream binary sensors (`server_domain_data_stream`, `server_enrichment_stream`) are "on"

---

## S13 — Multi-Cycle: TriggerPoll × 2 (Opus)

1. Record initial `monthly_used` from `GetStatus`
2. `TriggerPoll` location "Lucerne" → `triggeredCount` ≥ 1
3. Wait 30s for poll round to complete
4. Verify `monthly_used` increased (expect increment = sum of call weights of the resolved domains, 11 for the e2e config)

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
