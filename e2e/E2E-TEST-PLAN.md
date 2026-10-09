# E2E Test Plan — njord + ha-njord

Agent-orchestrated end-to-end test against a real Docker stack.
gRPC path only (no MQTT). Real Open-Meteo API.

## Section Index

| Section | Name | Steps | Agent |
|---------|------|-------|-------|
| S0 | Stack Setup | 5 | Opus |
| S1 | HA Onboarding + Token | 8 | Opus |
| S2 | Integration Setup | 6 | Opus |
| S3 | Entity Registration + Enable All | 8 | Opus |
| S4 | WeatherService: GetCatalog + Streaming | 8 | Haiku #1 (gRPC) |
| S5 | WeatherService: GetForecast | 5 | Haiku #1 (gRPC) |
| S6 | WeatherService: GetEnrichments | 7 | Haiku #1 (gRPC) |
| S7 | Entity Attribute Depth + Enrichment Validation | 22 | Haiku #1 (REST) |
| S8 | Connectivity Entities | 4 | Haiku #2 (REST) |
| S9 | Server Entities | 7 | Haiku #2 (REST) |
| S10 | OpsService: Full Coverage | 8 | Haiku #2 (gRPC) |
| S11 | gRPC + REST Error Handling | 8 | Haiku #2 |
| S12 | HA Verification: Weather Cards | 3 | Haiku #3 (browser/REST) |
| S13 | HA Verification: Enrichment Entities | 3 | Haiku #3 (browser/REST) |
| S14 | HA Verification: Server Entities | 2 | Haiku #3 (browser/REST) |
| S15 | SensorService: Push + StreamPush | 4 | Opus |
| S16 | Multi-Cycle: TriggerPoll × 2 | 6 | Opus |
| S17 | Budget Tracking Across Cycles | 4 | Opus |
| S18 | AdminService: GetConfig + StreamConfig | 5 | Opus |
| S19 | Config Mutation: Disable Alerts | 3 | Opus |
| S20 | Config Mutation: Verify Entity Removal | 3 | Opus |
| S21 | Config Mutation: Re-enable Alerts | 4 | Opus |
| S22 | Config Mutation: Change Horizons | 5 | Opus |
| S23 | Resilience: Container Restart | 7 | Opus |
| S24 | Teardown: Integration Removal | 5 | Opus |
| | **Total** | **~151** | |

---

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
- **Poll interval:** 3600 s (default)
- **Forecast days:** 4

### Expected Entities (~47)

Derived from config: 1 location × 2 models × all enrichments.

**Enabled by default (26):** weather (3), alerts (14), server sensors (4),
stream binary_sensors (3), event (1), button (1).
**Disabled by default (21):** indices (11), derived (5), trend (1), history (1),
inversion binary_sensor (1), target sensors (2). These exist in the HA entity
registry but require explicit enabling via `config/entity_registry/update`
(websocket) or the HA UI before they appear in `/api/states`.

| Platform | Count | Pattern | device_class | state_class | unit | Expected Values |
|----------|-------|---------|-------------|-------------|------|-----------------|
| weather | 3 | `lucerne_icon_d2`, `lucerne_ecmwf_ifs_0_25deg`, `lucerne_consensus` | — | — | — | state ∈ HA condition list |
| sensor (alerts) | 14 | `lucerne_{frost,heat,storm,heavy_rain,uv,fog,snow,pressure_drop,thunderstorm,ice,wind_chill,visibility,tropical_night,humidity}_alert` | — | — | — | severity ∈ {none,yellow,orange,red}, confidence 0–100 |
| sensor (indices) | 8 | `lucerne_{laundry,outdoor,running,cycling,bbq,irrigation,solar,night_ventilation}_index` | — | — | — | 0–100 |
| sensor (indices) | 3 | `lucerne_vpd`, `lucerne_frost_hours`, `lucerne_frost_confidence` | — | — | kPa, h, % | numeric |
| sensor (trends) | 1 | `lucerne_weather_trend` | — | — | — | non-empty string |
| sensor (derived) | 5 | `lucerne_{sunshine,diurnal_amplitude,beaufort,wind_chill,dewpoint_comfort}` | — | — | h, °C, Bft, °C, — | beaufort 0–12, sunshine ≥ 0 |
| sensor (history) | 1 | `lucerne_model_performance` | — | — | — | not unavailable |
| sensor (server) | 4 | `server_monthly_usage`, `server_daily_usage`, `server_version`, `server_uptime` | — | — | %, %, —, h | version=semver, usage=numeric |
| sensor (targets) | 2 | `server_icon_d2_lucerne`, `server_ecmwf_ifs025_lucerne` | timestamp | — | — | ISO timestamp |
| binary_sensor | 4 | `lucerne_inversion`, `server_forecast_stream`, `server_enrichment_stream`, `server_config_stream` | connectivity (streams) | — | — | on/off |
| event | 1 | `lucerne_weather_alert` | — | — | — | exists |
| button | 1 | `server_trigger_poll` | — | — | — | exists |

---

## S0 — Stack Setup

**Agent:** Opus (main)

| Step | Action | Pass Criteria |
|------|--------|---------------|
| 0.1 | `docker compose -f e2e/docker-compose.e2e.yml down -v` | No errors |
| 0.2 | `docker compose -f e2e/docker-compose.e2e.yml up -d --build` | Both containers start |
| 0.3 | Poll `http://localhost:8080/alive` until HTTP 200 (timeout: 60s) | njord healthy |
| 0.4 | Poll `http://localhost:8123/` until HTTP 200 or 302 (timeout: 120s) | HA healthy |
| 0.5 | Record startup duration (time from 0.2 to 0.4 complete) | Documented |

---

## S1 — HA Onboarding + Token

**Agent:** Opus (main), using claude-in-chrome (preferred) or HA API (fallback)

The user path is browser-based; the API fallback keeps the test runnable when
Chrome is unavailable. Both paths produce the same long-lived access token.

### S1A — HA Onboarding (first run only)

**Browser path (preferred):**

| Step | Action | Pass Criteria |
|------|--------|---------------|
| 1.1 | Open `http://localhost:8123` in browser | Onboarding page loads |
| 1.2 | Create user account (name: "njord-e2e", username: "njord", password: "e2e-test-2026") | Account created |
| 1.3 | Complete onboarding wizard (location: Lucerne 47.05/8.31, timezone: Europe/Zurich, telemetry opt-out) | Dashboard appears |

**API fallback** (if chrome extension unavailable):

| Step | Action | Pass Criteria |
|------|--------|---------------|
| 1.1f | `GET /api/onboarding` → check all steps `done: false` | Onboarding needed |
| 1.2f | `POST /api/onboarding/users` with `{"client_id":"http://localhost:8123/","name":"njord-e2e","username":"njord","password":"e2e-test-2026","language":"en"}` | `auth_code` returned |
| 1.3f | Exchange auth_code via `POST /auth/token`, then complete `core_config`, `analytics`, `integration` onboarding steps | All steps completed |

### S1B — Create Long-Lived Access Token

**Browser path (preferred):**

| Step | Action | Pass Criteria |
|------|--------|---------------|
| 1.4 | Navigate to user profile (click user icon → profile) | Profile page loads |
| 1.5 | Scroll to "Long-Lived Access Tokens" section | Section visible |
| 1.6 | Click "Create Token", name: "e2e-test" | Token displayed |
| 1.7 | Copy token value, store for REST API calls | Token captured |

**API fallback** (if chrome extension unavailable):

| Step | Action | Pass Criteria |
|------|--------|---------------|
| 1.4f | Connect websocket `ws://localhost:8123/api/websocket`, authenticate with short-lived access token | `auth_ok` |
| 1.5f | Send `{"type":"auth/long_lived_access_token","client_name":"e2e-test","lifespan":365}` | Token returned |

**Common:**

| Step | Action | Pass Criteria |
|------|--------|---------------|
| 1.8 | All subsequent REST calls use `Authorization: Bearer <token>` | — |

---

## S2 — Integration Setup

**Agent:** Opus (main), using claude-in-chrome (preferred) or HA API (fallback)

**Browser path (preferred):**

| Step | Action | Pass Criteria |
|------|--------|---------------|
| 2.1 | Navigate to Settings → Devices & Services | Integrations page loads |
| 2.2 | Click "Add Integration" | Integration search dialog opens |
| 2.3 | Search for "njord" | njord integration appears |
| 2.4 | Select njord, enter host: `njord`, port: `8081` | Config flow form visible |
| 2.5 | Submit | Success message with location/model counts |
| 2.6 | Record time from submit to success | Documented |

**API fallback** (if chrome extension unavailable):

| Step | Action | Pass Criteria |
|------|--------|---------------|
| 2.1f | `POST /api/config/config_entries/flow` with `{"handler":"njord","show_advanced_options":false}` | `flow_id` + `data_schema` returned |
| 2.2f | `POST /api/config/config_entries/flow/<flow_id>` with `{"host":"njord","port":8081}` | `type: "create_entry"`, `state: "loaded"` |
| 2.3f | Record `entry_id` from response (needed for S24 teardown) | Entry ID captured |
| 2.4f | Verify `description_placeholders` shows location/model counts | Counts match config |

---

## S3 — Entity Registration + Enable All

**Agent:** Opus (main), using HA REST API + websocket

### S3A — Default Entity Set (what the user gets out of the box)

| Step | Action | Pass Criteria |
|------|--------|---------------|
| 3.1 | Poll `GET /api/states` until weather entities appear (interval: 5s, timeout: 120s) | At least 1 `weather.*` entity exists |
| 3.2 | Count njord entities in `/api/states` | ~26 entities (enabled-by-default set) |
| 3.3 | Verify 3 weather entities: `weather.lucerne_icon_d2`, `weather.lucerne_ecmwf_ifs_0_25deg`, `weather.lucerne_consensus` | All 3 present |
| 3.4 | Verify 14 alert sensors present (enabled by default) | All 14 in `/api/states` |
| 3.5 | Record time from integration setup (2.5/2.2f) to first entities | Documented |

### S3B — Entity Registry + Enable All (for subsequent test sections)

| Step | Action | Pass Criteria |
|------|--------|---------------|
| 3.6 | Query entity registry via websocket (`config/entity_registry/list`), filter `platform: "njord"` | ~47 total entities (enabled + disabled) |
| 3.7 | Enable all entities with `disabled_by: "integration"` via websocket `config/entity_registry/update` (set `disabled_by: null`) | All 21 disabled entities enabled |
| 3.8 | Reload integration via `POST /api/config/config_entries/entry/<entry_id>/reload`, then poll `/api/states` until ~47 njord entities appear (timeout: 30s) | All entities visible |

---

## S4 — WeatherService: GetCatalog + Streaming

**Agent:** Haiku #1

> **Note:** gRPC reflection is not enabled. All `grpcurl` calls require
> `-import-path protos -proto njord/v2/<service>.proto`. Proto files live at
> `protos/njord/v2/` (weather.proto, ops.proto, sensor.proto, admin.proto,
> common.proto).

| Step | Action | Pass Criteria |
|------|--------|---------------|
| 4.1 | `grpcurl -plaintext -import-path protos -proto njord/v2/weather.proto localhost:8081 njord.v2.WeatherService/GetCatalog` | Response received |
| 4.2 | Verify response `locations` array contains entry with `name: "lucerne"` | Present |
| 4.3 | Verify location entry has `models` containing `icon_d2` and `ecmwf_ifs025` | Both present |
| 4.4 | Verify `models` array has entries with `id`, `displayName`, `provider`, `coverageTier` | Fields populated |
| 4.5 | Verify model `icon_d2` has `coverageTier` = `COVERAGE_TIER_REGIONAL` | Correct tier |
| 4.6 | Verify model `ecmwf_ifs025` has `coverageTier` = `COVERAGE_TIER_GLOBAL` | Correct tier |
| 4.7 | `timeout 30 grpcurl -plaintext -import-path protos -proto njord/v2/weather.proto -d '{"location":"lucerne"}' localhost:8081 njord.v2.WeatherService/StreamForecasts` | At least 1 `ForecastUpdate` message received before timeout |
| 4.8 | Open `StreamEnrichments` in background, then trigger a poll via `grpcurl ... njord.v2.OpsService/TriggerPoll`, wait up to 60s for the stream to deliver at least 1 `EnrichmentEvent` (the stream only pushes events after a poll produces new enrichment data) | At least 1 `EnrichmentEvent` received |

---

## S5 — WeatherService: GetForecast

**Agent:** Haiku #1

| Step | Action | Pass Criteria |
|------|--------|---------------|
| 5.1 | `grpcurl -plaintext -import-path protos -proto njord/v2/weather.proto -d '{"location":"lucerne","model":"icon_d2"}' localhost:8081 njord.v2.WeatherService/GetForecast` | Response contains `hourly` array |
| 5.2 | Verify `hourly` entries have `temperature`, `humidity`, `windSpeed` with numeric values | All present and numeric |
| 5.3 | Verify `hourly` timestamps (`validAt`) span at least 48 h from now (icon_d2 coverage) | Horizon coverage confirmed |
| 5.4 | Verify `daily` entries have `temperatureMax`, `temperatureMin`, `precipitationSum` | Fields present |
| 5.5 | `grpcurl -plaintext -import-path protos -proto njord/v2/weather.proto -d '{"location":"lucerne","model":"ecmwf_ifs025"}' localhost:8081 njord.v2.WeatherService/GetForecast` | Response has `hourly` array, timestamps span ≥ 72 h |

---

## S6 — WeatherService: GetEnrichments

**Agent:** Haiku #1

| Step | Action | Pass Criteria |
|------|--------|---------------|
| 6.1 | `grpcurl -plaintext -import-path protos -proto njord/v2/weather.proto -d '{"location":"lucerne"}' localhost:8081 njord.v2.WeatherService/GetEnrichments` | Response received |
| 6.2 | Verify `alerts` field present with `alerts` array | Alerts populated |
| 6.3 | Verify `indices` field present with `days` array containing `dayScoreSet` entries | Indices populated |
| 6.4 | Verify `trends` field present with `parameterTrends` array | Trends populated |
| 6.5 | Verify `derived` field present with `byHorizon` and `scalars` | Derived populated |
| 6.6 | Verify `history` field present with `models` array | History populated |
| 6.7 | Verify `consensus` field present with `hourlyParameters` array | Consensus populated |

---

## S7 — Entity Attribute Depth + Enrichment Validation

**Agent:** Haiku #1

### Weather Entities

| Step | Action | Pass Criteria |
|------|--------|---------------|
| 7.1 | `GET /api/states/weather.lucerne_icon_d2` | State ≠ "unavailable", state ∈ HA condition list (sunny, cloudy, rainy, etc.) |
| 7.2 | Verify attributes: `temperature` is numeric, `temperature_unit` = "°C" | Both correct |
| 7.3 | Verify attributes: `wind_speed` is numeric, `native_wind_speed_unit` = "m/s" (HA may convert display `wind_speed_unit` to the user's unit system, e.g. km/h) | Both correct |
| 7.4 | Verify attributes: `humidity` is numeric (0–100) | In range |
| 7.5 | `POST /api/services/weather/get_forecasts` with `entity_id: weather.lucerne_icon_d2, type: hourly` | Forecast entries returned |
| 7.6 | Verify forecast entries have `temperature`, `humidity`, `wind_speed` keys | All keys present |

### Consensus Validation

| Step | Action | Pass Criteria |
|------|--------|---------------|
| 7.7 | `GET /api/states/weather.lucerne_consensus` | State ≠ "unavailable" |
| 7.8 | Verify attribute `agreement` is numeric, 0–100 (%) | In range |
| 7.9 | Verify attribute `spread` is numeric, ≥ 0 (°C) | Non-negative |
| 7.10 | Verify attribute `available_models` is integer, ≥ 2 | At least 2 models |
| 7.11 | Get icon_d2 and ecmwf_ifs025 temperatures; verify consensus temperature is within [min, max] of model temperatures | Within model range |

### Alert Sensors

| Step | Action | Pass Criteria |
|------|--------|---------------|
| 7.12 | Verify 14 alert sensors exist (`sensor.lucerne_*_alert`) | All 14 present |
| 7.13 | For each alert sensor: verify attribute `severity` ∈ {none, yellow, orange, red} | Valid enum |
| 7.14 | For each alert sensor: verify attribute `confidence` is numeric 0–100 (note: `trigger_value` and `threshold` are present only when severity ≠ "none") | In range |
| 7.15 | Verify at least 1 alert sensor has `severity` ≠ "none" (plausibility: some alert should fire for any weather) | At least one active alert |

### Index Sensors

| Step | Action | Pass Criteria |
|------|--------|---------------|
| 7.16 | Verify 8 index sensors exist (`sensor.lucerne_*_index`) | All 8 present |
| 7.17 | For each index sensor: verify state is numeric 0–100 | In range |
| 7.18 | Verify `sensor.lucerne_vpd` state is numeric (kPa) | Numeric |
| 7.19 | Verify `sensor.lucerne_frost_hours` state is numeric or "unavailable" (requires history data from multiple cycles) | Numeric or unavailable |

### Derived, Trend, History Sensors

| Step | Action | Pass Criteria |
|------|--------|---------------|
| 7.20 | Verify 5 derived sensors exist; `beaufort` state is numeric 0–12, `sunshine` ≥ 0 | In range |
| 7.21 | Verify `sensor.lucerne_weather_trend` state is non-empty descriptive string | Not empty |
| 7.22 | Verify `sensor.lucerne_model_performance` state ≠ "unavailable" | Not unavailable |

---

## S8 — Connectivity Entities

**Agent:** Haiku #2

| Step | Action | Pass Criteria |
|------|--------|---------------|
| 8.1 | `GET /api/states/binary_sensor.server_forecast_stream` | State = "on" |
| 8.2 | `GET /api/states/binary_sensor.server_enrichment_stream` | State = "on" |
| 8.3 | `GET /api/states/binary_sensor.server_config_stream` | State = "on" |
| 8.4 | `GET /api/states/binary_sensor.lucerne_inversion` | State ∈ {"on", "off"} |

---

## S9 — Server Entities

**Agent:** Haiku #2

| Step | Action | Pass Criteria |
|------|--------|---------------|
| 9.1 | `GET /api/states/sensor.server_version` | State matches semver pattern (e.g., "0.1.0" or "0.0.0-dev") |
| 9.2 | `GET /api/states/sensor.server_uptime` | State is numeric (hours) |
| 9.3 | `GET /api/states/sensor.server_monthly_usage` | State is numeric (percentage of monthly budget) |
| 9.4 | `GET /api/states/sensor.server_daily_usage` | State is numeric (percentage of daily budget) |
| 9.5 | `GET /api/states/button.server_trigger_poll` | Entity exists |
| 9.6 | `GET /api/states/sensor.server_icon_d2_lucerne` | State is ISO timestamp or "unknown" |
| 9.7 | `GET /api/states/sensor.server_ecmwf_ifs025_lucerne` | State is ISO timestamp or "unknown" |

---

## S10 — OpsService: Full Coverage

**Agent:** Haiku #2

| Step | Action | Pass Criteria |
|------|--------|---------------|
| 10.1 | `grpcurl -plaintext -import-path protos -proto njord/v2/ops.proto localhost:8081 njord.v2.OpsService/GetStatus` | Response contains `version` field |
| 10.2 | Verify response `version` matches semver pattern | Valid version |
| 10.3 | Verify `models` array contains entries with `location: "lucerne"` | Present |
| 10.4 | Verify models include `icon_d2` and `ecmwf_ifs025` | Both present |
| 10.5 | Verify `activeEnrichments` contains all 6 feature names | All present |
| 10.6 | Verify `budget` has `monthlyLimit`, `monthlyUsed`, `dailyLimit`, `dailyUsed`, `usagePercent` | All fields present |
| 10.7 | `grpcurl -plaintext -import-path protos -proto njord/v2/ops.proto localhost:8081 njord.v2.OpsService/GetTargets` | Response contains `targets` array with ≥ 2 entries |
| 10.8 | Verify each target has `location`, `model`, `phase`, `nextPoll` fields | Fields populated |

---

## S11 — gRPC + REST Error Handling

**Agent:** Haiku #2

### gRPC Errors

| Step | Action | Pass Criteria |
|------|--------|---------------|
| 11.1 | `grpcurl -plaintext -import-path protos -proto njord/v2/weather.proto -d '{"location":"nonexistent","model":"icon_d2"}' localhost:8081 njord.v2.WeatherService/GetForecast` | gRPC error (NOT_FOUND or INVALID_ARGUMENT), not a crash |
| 11.2 | `grpcurl -plaintext -import-path protos -proto njord/v2/weather.proto -d '{"location":"lucerne","model":"invalid_model_xyz"}' localhost:8081 njord.v2.WeatherService/GetForecast` | gRPC error (NOT_FOUND or INVALID_ARGUMENT) |
| 11.3 | `grpcurl -plaintext -import-path protos -proto njord/v2/weather.proto -d '{"location":""}' localhost:8081 njord.v2.WeatherService/GetEnrichments` | gRPC error (INVALID_ARGUMENT) |
| 11.4 | `grpcurl -plaintext -import-path protos -proto njord/v2/sensor.proto -d '{"kind":"SENSOR_KIND_UNSPECIFIED","location":"lucerne","source":"test","value":21.5}' localhost:8081 njord.v2.SensorService/Push` | Rejection: `accepted` = false or gRPC error |
| 11.5 | `grpcurl -plaintext -import-path protos -proto njord/v2/admin.proto -d '{"locations":[]}' localhost:8081 njord.v2.AdminService/SetLocations` | Rejection: `applied` = false with `rejectionReason`, or gRPC error |
| 11.6 | `grpcurl -plaintext -import-path protos -proto njord/v2/admin.proto -d '{"requestsPerMonth":0}' localhost:8081 njord.v2.AdminService/SetBudget` | Rejection or warning in response |

### HA REST API Errors

| Step | Action | Pass Criteria |
|------|--------|---------------|
| 11.7 | `GET /api/states/sensor.nonexistent_njord_entity_xyz` | HTTP 404 or empty/not-found response, no crash |
| 11.8 | `POST /api/services/weather/get_forecasts` with `{"entity_id": "weather.nonexistent_xyz"}` | Error response, HA stays healthy (verify `GET /api/` still responds) |

---

## S12 — HA Verification: Weather Cards

**Agent:** Haiku #3, using claude-in-chrome (preferred) or HA REST API (fallback)

Browser: Developer Tools → States. API fallback: `GET /api/states/<entity_id>`.

| Step | Action | Pass Criteria |
|------|--------|---------------|
| 12.1 | Filter by `weather.lucerne` | 3 weather entities visible |
| 12.2 | Inspect `weather.lucerne_icon_d2` | Attributes: temperature (numeric), humidity (numeric), wind_speed (numeric) |
| 12.3 | Inspect `weather.lucerne_consensus` | Attributes: agreement (numeric), spread (numeric), available_models (integer ≥ 2) |

---

## S13 — HA Verification: Enrichment Entities

**Agent:** Haiku #3, using claude-in-chrome (preferred) or HA REST API (fallback)

| Step | Action | Pass Criteria |
|------|--------|---------------|
| 13.1 | Filter by `sensor.lucerne_` | Enrichment entities visible (alerts, indices, derived, trends, history) |
| 13.2 | Inspect an active alert sensor (one with severity ≠ "none", e.g. `sensor.lucerne_heavy_rain_alert`) | Attributes: severity, confidence; `trigger_value` and `threshold` present when severity ≠ "none" |
| 13.3 | Inspect an index sensor (e.g., `sensor.lucerne_outdoor_index`) | State is numeric 0–100 |

---

## S14 — HA Verification: Server Entities

**Agent:** Haiku #3, using claude-in-chrome (preferred) or HA REST API (fallback)

| Step | Action | Pass Criteria |
|------|--------|---------------|
| 14.1 | Filter by `sensor.server` | Server entities visible (version, uptime, monthly_usage, daily_usage, targets) |
| 14.2 | Verify `sensor.server_version` displays a version string, `sensor.server_monthly_usage` and `sensor.server_daily_usage` display numeric values | Values displayed |

---

## S15 — SensorService: Push + StreamPush

**Agent:** Opus (main)

| Step | Action | Pass Criteria |
|------|--------|---------------|
| 15.1 | `grpcurl -plaintext -import-path protos -proto njord/v2/sensor.proto -d '{"kind":"SENSOR_KIND_INDOOR_TEMPERATURE","location":"lucerne","source":"e2e-test","value":21.5,"measuredAt":"<now-ISO>"}' localhost:8081 njord.v2.SensorService/Push` | `accepted` = true |
| 15.2 | `grpcurl -plaintext -import-path protos -proto njord/v2/sensor.proto -d '{"kind":"SENSOR_KIND_INDOOR_HUMIDITY","location":"lucerne","source":"e2e-test","value":55.0,"measuredAt":"<now-ISO>"}' localhost:8081 njord.v2.SensorService/Push` | `accepted` = true |
| 15.3 | Verify both readings accepted (no `rejectionReason`) | Both accepted |
| 15.4 | Push with unknown location: `grpcurl -plaintext -import-path protos -proto njord/v2/sensor.proto -d '{"kind":"SENSOR_KIND_INDOOR_TEMPERATURE","location":"nonexistent","source":"e2e-test","value":21.5}' localhost:8081 njord.v2.SensorService/Push` | Rejection: `accepted` = false |

---

## S16 — Multi-Cycle: TriggerPoll × 2

**Agent:** Opus (main)

Cycle completion is tracked via `budget.dailyUsed` from gRPC `GetStatus` (and
the matching HA sensor `sensor.server_daily_usage`). HA entity `last_updated`
is unreliable for this purpose: HA only advances it when the state string
actually changes, which may not happen when Open-Meteo returns identical data.

| Step | Action | Pass Criteria |
|------|--------|---------------|
| 16.1 | Record `budget.dailyUsed` via `grpcurl -plaintext -import-path protos -proto njord/v2/ops.proto localhost:8081 njord.v2.OpsService/GetStatus` | Baseline captured |
| 16.2 | `grpcurl -plaintext -import-path protos -proto njord/v2/ops.proto -d '{}' localhost:8081 njord.v2.OpsService/TriggerPoll` | `triggeredCount` ≥ 2 |
| 16.3 | Wait 15–20s for Open-Meteo fetch, then read `budget.dailyUsed` again via GetStatus | `dailyUsed` increased (delta = number of models × API calls per model, typically +4 per model) |
| 16.4 | Trigger second poll: `grpcurl ... njord.v2.OpsService/TriggerPoll` | `triggeredCount` ≥ 2 |
| 16.5 | Wait 15–20s, read `budget.dailyUsed` via GetStatus | `dailyUsed` increased again by same delta — two cycles confirmed |
| 16.6 | Verify `weather.lucerne_icon_d2` state ≠ "unavailable" after both cycles | Weather entity still healthy |

---

## S17 — Budget Tracking Across Cycles

**Agent:** Opus (main)

| Step | Action | Pass Criteria |
|------|--------|---------------|
| 17.1 | Record `sensor.server_daily_usage` state before S16 cycles | Usage captured |
| 17.2 | After S16 cycles complete, read `sensor.server_daily_usage` again (wait for ha-njord's 30s status poll if needed) | Usage > previous value |
| 17.3 | Read `budget.dailyUsed` from S16's final GetStatus call | Numeric, > 0 |
| 17.4 | Verify gRPC `dailyUsed` is consistent with HA `sensor.server_daily_usage` (HA shows percentage of daily budget = `dailyUsed / dailyLimit * 100`) | Consistent |

---

## S18 — AdminService: GetConfig + StreamConfig

**Agent:** Opus (main)

| Step | Action | Pass Criteria |
|------|--------|---------------|
| 18.1 | `grpcurl -plaintext -import-path protos -proto njord/v2/admin.proto localhost:8081 njord.v2.AdminService/GetConfig` | Response received |
| 18.2 | Verify `locations` contains "lucerne" with lat 47.05, lon 8.31 | Matches Docker config |
| 18.3 | Verify `defaultModels` contains "icon_d2" and "ecmwf_ifs025" | Matches Docker config |
| 18.4 | Verify `horizons` contains [3, 6, 12, 24, 48, 72] (may include duplicates from env + default merge) | All configured horizons present |
| 18.5 | Verify `enrichment` shows all 6 features enabled (`consensus.enabled`, `alerts.enabled`, etc.) | All enabled |

---

## S19 — Config Mutation: Disable Alerts

**Agent:** Opus (main)

| Step | Action | Pass Criteria |
|------|--------|---------------|
| 19.1 | `grpcurl -plaintext -import-path protos -proto njord/v2/admin.proto -d '{"alerts":{"enabled":false}}' localhost:8081 njord.v2.AdminService/SetEnrichment` | `applied` = true |
| 19.2 | `grpcurl -plaintext -import-path protos -proto njord/v2/admin.proto localhost:8081 njord.v2.AdminService/GetConfig` → verify `enrichment.alerts.enabled` = false | Config updated |
| 19.3 | `grpcurl -plaintext -import-path protos -proto njord/v2/ops.proto -d '{}' localhost:8081 njord.v2.OpsService/TriggerPoll` | Poll triggered |

---

## S20 — Config Mutation: Verify Entity Removal

**Agent:** Opus (main)

| Step | Action | Pass Criteria |
|------|--------|---------------|
| 20.1 | Poll `GET /api/states` until alert sensors become "unavailable" or disappear (interval: 5s, timeout: 60s) | Alert sensors gone/unavailable |
| 20.2 | Count remaining njord entities; verify count dropped by ~15 (14 alerts + 1 event) | Entity count reduced |
| 20.3 | Verify specific entity `sensor.lucerne_frost_alert` is unavailable or absent | Confirmed gone |

---

## S21 — Config Mutation: Re-enable Alerts

**Agent:** Opus (main)

| Step | Action | Pass Criteria |
|------|--------|---------------|
| 21.1 | `grpcurl -plaintext -import-path protos -proto njord/v2/admin.proto -d '{"alerts":{"enabled":true}}' localhost:8081 njord.v2.AdminService/SetEnrichment` | `applied` = true |
| 21.2 | `grpcurl -plaintext -import-path protos -proto njord/v2/ops.proto -d '{}' localhost:8081 njord.v2.OpsService/TriggerPoll` | Poll triggered |
| 21.3 | Poll `GET /api/states` until 14 alert sensors reappear with state ≠ "unavailable" (timeout: 120s) | All 14 restored |
| 21.4 | Verify total entity count matches pre-S19 count | Count restored |

---

## S22 — Config Mutation: Change Horizons

**Agent:** Opus (main)

| Step | Action | Pass Criteria |
|------|--------|---------------|
| 22.1 | `grpcurl -plaintext -import-path protos -proto njord/v2/admin.proto -d '{"horizons":[6,24]}' localhost:8081 njord.v2.AdminService/SetSettings` | `applied` = true |
| 22.2 | `grpcurl -plaintext -import-path protos -proto njord/v2/admin.proto localhost:8081 njord.v2.AdminService/GetConfig` → verify `horizons` includes 6 and 24 | Config updated |
| 22.3 | `grpcurl -plaintext -import-path protos -proto njord/v2/ops.proto -d '{}' localhost:8081 njord.v2.OpsService/TriggerPoll` | Poll triggered |
| 22.4 | Wait 15–20s, then verify GetConfig shows changed horizons | Horizons changed |
| 22.5 | Restore: `grpcurl -plaintext -import-path protos -proto njord/v2/admin.proto -d '{"horizons":[3,6,12,24,48,72]}' localhost:8081 njord.v2.AdminService/SetSettings` | `applied` = true, horizons restored |

---

## S23 — Resilience: Container Restart

**Agent:** Opus (main)

| Step | Action | Pass Criteria |
|------|--------|---------------|
| 23.1 | `docker stop njord-e2e` | Container stopped |
| 23.2 | Wait 10s, then `GET /api/states/binary_sensor.server_forecast_stream` | State = "off" (stream lost) |
| 23.3 | Verify `weather.lucerne_icon_d2` still has its last known state (HA retains the value; may not show "unavailable" until `expire_after` elapses) | State present |
| 23.4 | `docker start njord-e2e` | Container starts |
| 23.5 | Poll `http://localhost:8080/alive` until HTTP 200 (timeout: 60s) | njord healthy again |
| 23.6 | Poll `GET /api/states/binary_sensor.server_forecast_stream` until state = "on" (timeout: 120s) | gRPC stream reconnected |
| 23.7 | Verify `weather.lucerne_icon_d2` state ≠ "unavailable" | Entity recovered |

---

## S24 — Teardown: Integration Removal

**Agent:** Opus (main), using claude-in-chrome (preferred) or HA API (fallback)

**Browser path (preferred):**

| Step | Action | Pass Criteria |
|------|--------|---------------|
| 24.1 | Navigate to Settings → Devices & Services | Integrations page loads |
| 24.2 | Find njord integration, click "Delete" / remove | Deletion confirmed |

**API fallback** (if chrome extension unavailable):

| Step | Action | Pass Criteria |
|------|--------|---------------|
| 24.1f | `DELETE /api/config/config_entries/entry/<entry_id>` (entry_id from S2) | `require_restart: false` |

**Common:**

| Step | Action | Pass Criteria |
|------|--------|---------------|
| 24.3 | Poll `GET /api/states` until no njord entities remain (interval: 5s, timeout: 30s) | All njord entities gone |
| 24.4 | Verify 0 entities match njord platform | No orphans |
| 24.5 | `docker compose -f e2e/docker-compose.e2e.yml down -v` | Stack torn down |

---

## Orchestration

```
┌──────────────────────────────────────────────────────────────────┐
│  Opus Main Agent                                                  │
│  S0: Docker up, health poll                                 [SEQ] │
│  S1: Browser — onboarding, token                            [SEQ] │
│  S2: Browser — integration setup                            [SEQ] │
│  S3: Entity registration + enable all                        [SEQ] │
│                                                                   │
│  ── Parallel Block A (after S3) ────────────────────────────────  │
│  ┌───────────────────┐  ┌───────────────────┐  ┌──────────────┐  │
│  │  Haiku #1         │  │  Haiku #2         │  │  Haiku #3    │  │
│  │  S4: GetCatalog   │  │  S8: Connectivity │  │  S12: Weather│  │
│  │  S5: GetForecast  │  │  S9: Server       │  │  S13: Enrich │  │
│  │  S6: GetEnrich    │  │  S10: OpsService  │  │  S14: Server │  │
│  │  S7: Attr Depth   │  │  S11: Errors      │  │              │  │
│  └───────────────────┘  └───────────────────┘  └──────────────┘  │
│                                                                   │
│  ── Re-validate any Haiku FAILs ────────────────────────────────  │
│                                                                   │
│  S15: SensorService Push                                    [SEQ] │
│  S16: Multi-Cycle TriggerPoll × 2                           [SEQ] │
│  S17: Budget Tracking                                       [SEQ] │
│  S18: AdminService GetConfig                                [SEQ] │
│  S19: Config Mutation: Disable Alerts                       [SEQ] │
│  S20: Verify Entity Removal                                 [SEQ] │
│  S21: Re-enable Alerts                                      [SEQ] │
│  S22: Change Horizons                                       [SEQ] │
│  S23: Resilience: Container Restart                         [SEQ] │
│  S24: Teardown: Integration Removal                         [SEQ] │
│                                                                   │
│  ── Write Results ──────────────────────────────────────────────  │
└──────────────────────────────────────────────────────────────────┘
```

### Subagent Briefing

Each Haiku subagent receives:
- The HA base URL (`http://localhost:8123`)
- The long-lived access token
- Their assigned sections from this plan (full step tables)
- Instruction to report PASS/FAIL per step with detail

**Haiku #1 — Weather + Entities (S4–S7):**
Verify all WeatherService RPCs (GetCatalog, GetForecast, GetEnrichments, StreamForecasts, StreamEnrichments) and deep entity attribute validation. Use grpcurl with `-import-path protos -proto njord/v2/weather.proto` for gRPC calls and HA REST API for entity checks. Report ~42 steps.

**Haiku #2 — Connectivity + Ops + Errors (S8–S11):**
Verify connectivity/server entities via HA REST API, all OpsService RPCs (GetStatus, GetTargets), and error handling (invalid gRPC requests, HA REST errors). Use `-import-path protos -proto njord/v2/<service>.proto` for all grpcurl calls. Report ~27 steps.

**Haiku #3 — HA Verification (S12–S14):**
Verify entity display using claude-in-chrome (preferred) or HA REST API (fallback). Inspect weather, enrichment, and server entities. Report ~8 steps.

The main agent re-checks any FAIL before accepting it (Haiku may misinterpret API responses).

---

## Setup Recipes

### Default Recipe (Baseline)

Applied automatically by Docker config. No grpcurl commands needed.

- **Location:** lucerne (47.05, 8.31)
- **Models:** icon_d2, ecmwf_ifs025
- **Horizons:** [3, 6, 12, 24, 48, 72]
- **Enrichments:** all 6 enabled
- **Expected entities:** ~47

### Disabled-Alerts Recipe

Applied in S19, restored in S21. All grpcurl commands require `-import-path protos -proto njord/v2/<service>.proto`.

```bash
# Apply
grpcurl -plaintext -import-path protos -proto njord/v2/admin.proto -d '{"alerts":{"enabled":false}}' localhost:8081 njord.v2.AdminService/SetEnrichment
grpcurl -plaintext -import-path protos -proto njord/v2/ops.proto -d '{}' localhost:8081 njord.v2.OpsService/TriggerPoll

# Restore
grpcurl -plaintext -import-path protos -proto njord/v2/admin.proto -d '{"alerts":{"enabled":true}}' localhost:8081 njord.v2.AdminService/SetEnrichment
grpcurl -plaintext -import-path protos -proto njord/v2/ops.proto -d '{}' localhost:8081 njord.v2.OpsService/TriggerPoll
```

- **Expected change:** -15 entities (14 alert sensors + 1 weather_alert event)

### Changed-Horizons Recipe

Applied in S22, restored at end of S22.

```bash
# Apply
grpcurl -plaintext -import-path protos -proto njord/v2/admin.proto -d '{"horizons":[6,24]}' localhost:8081 njord.v2.AdminService/SetSettings
grpcurl -plaintext -import-path protos -proto njord/v2/ops.proto -d '{}' localhost:8081 njord.v2.OpsService/TriggerPoll

# Restore
grpcurl -plaintext -import-path protos -proto njord/v2/admin.proto -d '{"horizons":[3,6,12,24,48,72]}' localhost:8081 njord.v2.AdminService/SetSettings
grpcurl -plaintext -import-path protos -proto njord/v2/ops.proto -d '{}' localhost:8081 njord.v2.OpsService/TriggerPoll
```

- **Expected change:** forecast entries reflect 2 horizons instead of 6

---

## Results Format

Output: `e2e/results/E2E-TEST-RESULTS-<YYYY-MM-DD>.md`

### Summary Table

| Section | Step | Result | Detail |
|---------|------|--------|--------|
| S0 | 0.1 Clean shutdown | PASS | No errors |
| S0 | 0.2 Stack start | PASS | Both containers running |
| ... | ... | ... | ... |

Rules:
- Every step gets PASS or FAIL — **no SKIP allowed**
- FAIL includes the reason (error message, unexpected value, timeout)
- Timing observations are recorded in the Detail column

### Timing Baselines

Recorded on first run, used as reference for future runs:

| Metric | Baseline | Notes |
|--------|----------|-------|
| Stack startup, cached (0.2 → 0.4) | ~2s | Both containers instant when image cached |
| Stack startup, full build (0.2 → 0.4) | ~25s | Including Docker build |
| Integration → entities (2.5 → 3.1) | < 5s | gRPC fast, entities appear immediately |
| Poll cycle (TriggerPoll → budget increment) | ~10–15s | Open-Meteo fetch + processing |
| Config mutation → entity change | < 5s | Immediate via gRPC streams |
| Restart → stream reconnect (23.4 → 23.6) | < 5s | forecast_stream off → on |
| Teardown → entities gone (24.2 → 24.3) | < 1s | HA removes entities immediately |
| Full run duration | ~20 min | Including multi-cycle waits and ha-njord poll intervals |
