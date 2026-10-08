# E2E Test Plan — njord + ha-njord

Agent-orchestrated end-to-end test against a real Docker stack.
gRPC path only (no MQTT). Real Open-Meteo API.

## Section Index

| Section | Name | Steps | Agent |
|---------|------|-------|-------|
| S0 | Stack Setup | 5 | Opus |
| S1 | HA Onboarding + Token | 8 | Opus |
| S2 | Integration Setup | 6 | Opus |
| S3 | Entity Registration | 5 | Opus |
| S4 | WeatherService: GetCatalog + Streaming | 8 | Haiku #1 |
| S5 | WeatherService: GetForecast | 5 | Haiku #1 |
| S6 | WeatherService: GetEnrichments | 7 | Haiku #1 |
| S7 | Entity Attribute Depth + Enrichment Validation | 22 | Haiku #1 |
| S8 | Connectivity Entities | 4 | Haiku #2 |
| S9 | Server Entities | 7 | Haiku #2 |
| S10 | OpsService: Full Coverage | 8 | Haiku #2 |
| S11 | gRPC + REST Error Handling | 8 | Haiku #2 |
| S12 | HA Browser: Weather Cards | 3 | Haiku #3 |
| S13 | HA Browser: Enrichment Entities | 3 | Haiku #3 |
| S14 | HA Browser: Server Entities | 2 | Haiku #3 |
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

| Platform | Count | Pattern | device_class | state_class | unit | Expected Values |
|----------|-------|---------|-------------|-------------|------|-----------------|
| weather | 3 | `lucerne_icon_d2`, `lucerne_ecmwf_ifs_0_25deg`, `lucerne_consensus` | — | — | — | state ∈ HA condition list |
| sensor (alerts) | 14 | `lucerne_{frost,heat,storm,heavy_rain,uv,fog,snow,pressure_drop,thunderstorm,ice,wind_chill,visibility,tropical_night,humidity}_alert` | — | — | — | severity ∈ {none,yellow,orange,red}, confidence 0–100 |
| sensor (indices) | 8 | `lucerne_{laundry,outdoor,running,cycling,bbq,irrigation,solar,night_ventilation}_index` | — | — | — | 0–10 |
| sensor (indices) | 3 | `lucerne_vpd`, `lucerne_frost_hours`, `lucerne_frost_confidence` | — | — | kPa, h, % | numeric |
| sensor (trends) | 1 | `lucerne_weather_trend` | — | — | — | non-empty string |
| sensor (derived) | 5 | `lucerne_{sunshine,diurnal_amplitude,beaufort,wind_chill,dewpoint_comfort}` | — | — | h, °C, Bft, °C, — | beaufort 0–12, sunshine ≥ 0 |
| sensor (history) | 1 | `lucerne_model_performance` | — | — | — | not unavailable |
| sensor (server) | 4 | `server_monthly_usage`, `server_daily_usage`, `server_version`, `server_uptime` | — | — | requests, requests, —, — | version=semver, usage=numeric |
| sensor (targets) | 2 | `lucerne_icon_d2_target`, `lucerne_ecmwf_ifs_0_25deg_target` | timestamp | — | — | ISO timestamp |
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

**Agent:** Opus (main), using claude-in-chrome

### S1A — HA Onboarding (first run only)

| Step | Action | Pass Criteria |
|------|--------|---------------|
| 1.1 | Open `http://localhost:8123` in browser | Onboarding page loads |
| 1.2 | Create user account (name: "njord-e2e", username: "njord", password: "e2e-test-2026") | Account created |
| 1.3 | Complete onboarding wizard (location, timezone, telemetry opt-out) | Dashboard appears |

### S1B — Create Long-Lived Access Token

| Step | Action | Pass Criteria |
|------|--------|---------------|
| 1.4 | Navigate to user profile (click user icon → profile) | Profile page loads |
| 1.5 | Scroll to "Long-Lived Access Tokens" section | Section visible |
| 1.6 | Click "Create Token", name: "e2e-test" | Token displayed |
| 1.7 | Copy token value, store for REST API calls | Token captured |
| 1.8 | All subsequent REST calls use `Authorization: Bearer <token>` | — |

---

## S2 — Integration Setup

**Agent:** Opus (main), using claude-in-chrome

| Step | Action | Pass Criteria |
|------|--------|---------------|
| 2.1 | Navigate to Settings → Devices & Services | Integrations page loads |
| 2.2 | Click "Add Integration" | Integration search dialog opens |
| 2.3 | Search for "njord" | njord integration appears |
| 2.4 | Select njord, enter host: `njord`, port: `8081` | Config flow form visible |
| 2.5 | Submit | Success message with location/model counts |
| 2.6 | Record time from submit to success | Documented |

---

## S3 — Entity Registration

**Agent:** Opus (main), using HA REST API

| Step | Action | Pass Criteria |
|------|--------|---------------|
| 3.1 | Poll `GET /api/states` until weather entities appear (interval: 5s, timeout: 120s) | At least 1 `weather.*` entity exists |
| 3.2 | Count total entities with `njord` in device identifiers | ~47 entities (±5 tolerance) |
| 3.3 | Verify 3 weather entities: `weather.lucerne_icon_d2`, `weather.lucerne_ecmwf_ifs025`, `weather.lucerne_consensus` | All 3 present |
| 3.4 | Record time from integration setup (2.5) to first entities | Documented |
| 3.5 | Record complete entity ID list | Documented for future reference |

---

## S4 — WeatherService: GetCatalog + Streaming

**Agent:** Haiku #1

| Step | Action | Pass Criteria |
|------|--------|---------------|
| 4.1 | `grpcurl -plaintext localhost:8081 njord.v2.WeatherService/GetCatalog` | Response received |
| 4.2 | Verify response `locations` array contains entry with `name: "lucerne"` | Present |
| 4.3 | Verify location entry has `models` containing `icon_d2` and `ecmwf_ifs025` | Both present |
| 4.4 | Verify `models` array has entries with `id`, `displayName`, `provider`, `coverageTier` | Fields populated |
| 4.5 | Verify model `icon_d2` has `coverageTier` = `COVERAGE_TIER_REGIONAL` | Correct tier |
| 4.6 | Verify model `ecmwf_ifs025` has `coverageTier` = `COVERAGE_TIER_GLOBAL` | Correct tier |
| 4.7 | `timeout 30 grpcurl -plaintext -d '{"location":"lucerne"}' localhost:8081 njord.v2.WeatherService/StreamForecasts` | At least 1 `ForecastUpdate` message received before timeout |
| 4.8 | `timeout 30 grpcurl -plaintext -d '{"location":"lucerne"}' localhost:8081 njord.v2.WeatherService/StreamEnrichments` | At least 1 `EnrichmentEvent` message received before timeout |

---

## S5 — WeatherService: GetForecast

**Agent:** Haiku #1

| Step | Action | Pass Criteria |
|------|--------|---------------|
| 5.1 | `grpcurl -plaintext -d '{"location":"lucerne","model":"icon_d2"}' localhost:8081 njord.v2.WeatherService/GetForecast` | Response contains `hourly` array |
| 5.2 | Verify `hourly` entries have `temperature`, `humidity`, `windSpeed` with numeric values | All present and numeric |
| 5.3 | Verify `hourly` timestamps (`validAt`) span at least 48 h from now (icon_d2 coverage) | Horizon coverage confirmed |
| 5.4 | Verify `daily` entries have `temperatureMax`, `temperatureMin`, `precipitationSum` | Fields present |
| 5.5 | `grpcurl -plaintext -d '{"location":"lucerne","model":"ecmwf_ifs025"}' localhost:8081 njord.v2.WeatherService/GetForecast` | Response has `hourly` array, timestamps span ≥ 72 h |

---

## S6 — WeatherService: GetEnrichments

**Agent:** Haiku #1

| Step | Action | Pass Criteria |
|------|--------|---------------|
| 6.1 | `grpcurl -plaintext -d '{"location":"lucerne"}' localhost:8081 njord.v2.WeatherService/GetEnrichments` | Response received |
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
| 7.3 | Verify attributes: `wind_speed` is numeric, `wind_speed_unit` = "m/s" | Both correct |
| 7.4 | Verify attributes: `humidity` is numeric (0–100) | In range |
| 7.5 | `POST /api/services/weather/get_forecasts` with `entity_id: weather.lucerne_icon_d2, type: hourly` | Forecast entries returned |
| 7.6 | Verify forecast entries have `temperature`, `humidity`, `wind_speed` keys | All keys present |

### Consensus Validation

| Step | Action | Pass Criteria |
|------|--------|---------------|
| 7.7 | `GET /api/states/weather.lucerne_consensus` | State ≠ "unavailable" |
| 7.8 | Verify attribute `agreement` is numeric, 0–100 (%) | In range |
| 7.9 | Verify attribute `spread` is numeric, ≥ 0 (°C) | Non-negative |
| 7.10 | Verify attribute `models_used` is integer, ≥ 2 | At least 2 models |
| 7.11 | Get icon_d2 and ecmwf_ifs025 temperatures; verify consensus temperature is within [min, max] of model temperatures | Within model range |

### Alert Sensors

| Step | Action | Pass Criteria |
|------|--------|---------------|
| 7.12 | Verify 14 alert sensors exist (`sensor.lucerne_*_alert`) | All 14 present |
| 7.13 | For each alert sensor: verify attribute `severity` ∈ {none, yellow, orange, red} | Valid enum |
| 7.14 | For each alert sensor: verify attribute `confidence` is numeric 0–100 | In range |
| 7.15 | Verify at least 1 alert sensor has `severity` ≠ "none" (plausibility: some alert should fire for any weather) | At least one active alert |

### Index Sensors

| Step | Action | Pass Criteria |
|------|--------|---------------|
| 7.16 | Verify 8 index sensors exist (`sensor.lucerne_*_index`) | All 8 present |
| 7.17 | For each index sensor: verify state is numeric 0–10 | In range |
| 7.18 | Verify `sensor.lucerne_vpd` state is numeric (kPa) | Numeric |
| 7.19 | Verify `sensor.lucerne_frost_hours` state is numeric (hours) | Numeric |

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
| 8.1 | `GET /api/states/binary_sensor.forecast_stream` | State = "on" |
| 8.2 | `GET /api/states/binary_sensor.enrichment_stream` | State = "on" |
| 8.3 | `GET /api/states/binary_sensor.config_stream` | State = "on" |
| 8.4 | `GET /api/states/binary_sensor.lucerne_inversion` | State ∈ {"on", "off"} |

---

## S9 — Server Entities

**Agent:** Haiku #2

| Step | Action | Pass Criteria |
|------|--------|---------------|
| 9.1 | `GET /api/states/sensor.version` | State matches semver pattern (e.g., "0.1.0" or "0.1.0-alpha.1") |
| 9.2 | `GET /api/states/sensor.uptime` | State is non-empty (duration string or numeric) |
| 9.3 | `GET /api/states/sensor.monthly_usage` | State is numeric, attribute `unit_of_measurement` = "requests" |
| 9.4 | `GET /api/states/sensor.daily_usage` | State is numeric, attribute `unit_of_measurement` = "requests" |
| 9.5 | `GET /api/states/button.trigger_poll` | Entity exists |
| 9.6 | `GET /api/states/sensor.lucerne_icon_d2_target` | State is ISO timestamp or "unknown" |
| 9.7 | `GET /api/states/sensor.lucerne_ecmwf_ifs025_target` | State is ISO timestamp or "unknown" |

---

## S10 — OpsService: Full Coverage

**Agent:** Haiku #2

| Step | Action | Pass Criteria |
|------|--------|---------------|
| 10.1 | `grpcurl -plaintext localhost:8081 njord.v2.OpsService/GetStatus` | Response contains `version` field |
| 10.2 | Verify response `version` matches semver pattern | Valid version |
| 10.3 | Verify `models` array contains entries with `location: "lucerne"` | Present |
| 10.4 | Verify models include `icon_d2` and `ecmwf_ifs025` | Both present |
| 10.5 | Verify `activeEnrichments` contains all 6 feature names | All present |
| 10.6 | Verify `budget` has `monthlyLimit`, `monthlyUsed`, `dailyLimit`, `dailyUsed`, `usagePercent` | All fields present |
| 10.7 | `grpcurl -plaintext localhost:8081 njord.v2.OpsService/GetTargets` | Response contains `targets` array with ≥ 2 entries |
| 10.8 | Verify each target has `location`, `model`, `phase`, `nextPoll` fields | Fields populated |

---

## S11 — gRPC + REST Error Handling

**Agent:** Haiku #2

### gRPC Errors

| Step | Action | Pass Criteria |
|------|--------|---------------|
| 11.1 | `grpcurl -plaintext -d '{"location":"nonexistent","model":"icon_d2"}' localhost:8081 njord.v2.WeatherService/GetForecast` | gRPC error (NOT_FOUND or INVALID_ARGUMENT), not a crash |
| 11.2 | `grpcurl -plaintext -d '{"location":"lucerne","model":"invalid_model_xyz"}' localhost:8081 njord.v2.WeatherService/GetForecast` | gRPC error (NOT_FOUND or INVALID_ARGUMENT) |
| 11.3 | `grpcurl -plaintext -d '{"location":""}' localhost:8081 njord.v2.WeatherService/GetEnrichments` | gRPC error (INVALID_ARGUMENT) |
| 11.4 | `grpcurl -plaintext -d '{"kind":"SENSOR_KIND_UNSPECIFIED","location":"lucerne","source":"test","value":21.5}' localhost:8081 njord.v2.SensorService/Push` | Rejection: `accepted` = false or gRPC error |
| 11.5 | `grpcurl -plaintext -d '{"locations":[]}' localhost:8081 njord.v2.AdminService/SetLocations` | Rejection: `applied` = false with `rejectionReason`, or gRPC error |
| 11.6 | `grpcurl -plaintext -d '{"requestsPerMonth":0}' localhost:8081 njord.v2.AdminService/SetBudget` | Rejection or warning in response |

### HA REST API Errors

| Step | Action | Pass Criteria |
|------|--------|---------------|
| 11.7 | `GET /api/states/sensor.nonexistent_njord_entity_xyz` | HTTP 404 or empty/not-found response, no crash |
| 11.8 | `POST /api/services/weather/get_forecasts` with `{"entity_id": "weather.nonexistent_xyz"}` | Error response, HA stays healthy (verify `GET /api/` still responds) |

---

## S12 — HA Browser: Weather Cards

**Agent:** Haiku #3, using claude-in-chrome

| Step | Action | Pass Criteria |
|------|--------|---------------|
| 12.1 | Navigate to Developer Tools → States, filter by `weather.lucerne` | 3 weather entities visible |
| 12.2 | Click on `weather.lucerne_icon_d2` to expand | Attributes visible: temperature (numeric), humidity (numeric), wind_speed (numeric), condition icon |
| 12.3 | Click on `weather.lucerne_consensus` to expand | Attributes visible: agreement, spread, models_used |

---

## S13 — HA Browser: Enrichment Entities

**Agent:** Haiku #3, using claude-in-chrome

| Step | Action | Pass Criteria |
|------|--------|---------------|
| 13.1 | Filter states by `sensor.lucerne_` | Enrichment entities visible (alerts, indices, derived, trends, history) |
| 13.2 | Click on an alert sensor (e.g., `sensor.lucerne_frost_alert`) | Attributes visible: severity, confidence, trigger_value, threshold |
| 13.3 | Click on an index sensor (e.g., `sensor.lucerne_outdoor_index`) | State is numeric 0–10 |

---

## S14 — HA Browser: Server Entities

**Agent:** Haiku #3, using claude-in-chrome

| Step | Action | Pass Criteria |
|------|--------|---------------|
| 14.1 | Filter states by `sensor.version` or `sensor.server` or `sensor.uptime` | Server entities visible |
| 14.2 | Verify `sensor.version` displays a version string, `sensor.monthly_usage` and `sensor.daily_usage` display numeric values | Values displayed |

---

## S15 — SensorService: Push + StreamPush

**Agent:** Opus (main)

| Step | Action | Pass Criteria |
|------|--------|---------------|
| 15.1 | `grpcurl -plaintext -d '{"kind":"SENSOR_KIND_INDOOR_TEMPERATURE","location":"lucerne","source":"e2e-test","value":21.5,"measuredAt":"<now-ISO>"}' localhost:8081 njord.v2.SensorService/Push` | `accepted` = true |
| 15.2 | `grpcurl -plaintext -d '{"kind":"SENSOR_KIND_INDOOR_HUMIDITY","location":"lucerne","source":"e2e-test","value":55.0,"measuredAt":"<now-ISO>"}' localhost:8081 njord.v2.SensorService/Push` | `accepted` = true |
| 15.3 | Verify both readings accepted (no `rejectionReason`) | Both accepted |
| 15.4 | Push with unknown location: `grpcurl -plaintext -d '{"kind":"SENSOR_KIND_INDOOR_TEMPERATURE","location":"nonexistent","source":"e2e-test","value":21.5}' localhost:8081 njord.v2.SensorService/Push` | Rejection: `accepted` = false |

---

## S16 — Multi-Cycle: TriggerPoll × 2

**Agent:** Opus (main)

| Step | Action | Pass Criteria |
|------|--------|---------------|
| 16.1 | Record `last_updated` timestamp for `weather.lucerne_icon_d2` via `GET /api/states/weather.lucerne_icon_d2` | Timestamp captured |
| 16.2 | `grpcurl -plaintext -d '{}' localhost:8081 njord.v2.OpsService/TriggerPoll` | `triggeredCount` ≥ 2 |
| 16.3 | Poll `GET /api/states/weather.lucerne_icon_d2` until `last_updated` > captured timestamp (interval: 5s, timeout: 120s) | Timestamp advanced |
| 16.4 | Record new `last_updated`, trigger second poll: `grpcurl -plaintext -d '{}' localhost:8081 njord.v2.OpsService/TriggerPoll` | `triggeredCount` ≥ 2 |
| 16.5 | Poll until `last_updated` advances again (timeout: 120s) | Second cycle confirmed |
| 16.6 | Verify enrichment entity `sensor.lucerne_weather_trend` `last_updated` also advanced | Enrichments updated |

---

## S17 — Budget Tracking Across Cycles

**Agent:** Opus (main)

| Step | Action | Pass Criteria |
|------|--------|---------------|
| 17.1 | Record `sensor.daily_usage` state before cycle | Usage captured |
| 17.2 | After S16 cycles complete, read `sensor.daily_usage` again | Usage > previous value |
| 17.3 | `grpcurl -plaintext localhost:8081 njord.v2.OpsService/GetStatus` and extract `budget.dailyUsed` | Numeric, > 0 |
| 17.4 | Verify gRPC `dailyUsed` is consistent with HA `sensor.daily_usage` (within ±1, accounting for rounding) | Consistent |

---

## S18 — AdminService: GetConfig + StreamConfig

**Agent:** Opus (main)

| Step | Action | Pass Criteria |
|------|--------|---------------|
| 18.1 | `grpcurl -plaintext localhost:8081 njord.v2.AdminService/GetConfig` | Response received |
| 18.2 | Verify `locations` contains "lucerne" with lat 47.05, lon 8.31 | Matches Docker config |
| 18.3 | Verify `defaultModels` contains "icon_d2" and "ecmwf_ifs025" | Matches Docker config |
| 18.4 | Verify `horizons` = [3, 6, 12, 24, 48, 72] | Matches Docker config |
| 18.5 | Verify `enrichment` shows all 6 features enabled (`consensus.enabled`, `alerts.enabled`, etc.) | All enabled |

---

## S19 — Config Mutation: Disable Alerts

**Agent:** Opus (main)

| Step | Action | Pass Criteria |
|------|--------|---------------|
| 19.1 | `grpcurl -plaintext -d '{"alerts":{"enabled":false}}' localhost:8081 njord.v2.AdminService/SetEnrichment` | `applied` = true |
| 19.2 | `grpcurl -plaintext localhost:8081 njord.v2.AdminService/GetConfig` → verify `enrichment.alerts.enabled` = false | Config updated |
| 19.3 | `grpcurl -plaintext -d '{}' localhost:8081 njord.v2.OpsService/TriggerPoll` | Poll triggered |

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
| 21.1 | `grpcurl -plaintext -d '{"alerts":{"enabled":true}}' localhost:8081 njord.v2.AdminService/SetEnrichment` | `applied` = true |
| 21.2 | `grpcurl -plaintext -d '{}' localhost:8081 njord.v2.OpsService/TriggerPoll` | Poll triggered |
| 21.3 | Poll `GET /api/states` until 14 alert sensors reappear with state ≠ "unavailable" (timeout: 120s) | All 14 restored |
| 21.4 | Verify total entity count matches pre-S19 count | Count restored |

---

## S22 — Config Mutation: Change Horizons

**Agent:** Opus (main)

| Step | Action | Pass Criteria |
|------|--------|---------------|
| 22.1 | `grpcurl -plaintext -d '{"horizons":[6,24]}' localhost:8081 njord.v2.AdminService/SetSettings` | `applied` = true |
| 22.2 | `grpcurl -plaintext localhost:8081 njord.v2.AdminService/GetConfig` → verify `horizons` = [6, 24] | Config updated |
| 22.3 | `grpcurl -plaintext -d '{}' localhost:8081 njord.v2.OpsService/TriggerPoll` | Poll triggered |
| 22.4 | Wait 30s, then verify weather entity attributes reflect changed horizon set (fewer forecast detail items or updated horizon references) | Horizons changed |
| 22.5 | Restore: `grpcurl -plaintext -d '{"horizons":[3,6,12,24,48,72]}' localhost:8081 njord.v2.AdminService/SetSettings` | `applied` = true, horizons restored |

---

## S23 — Resilience: Container Restart

**Agent:** Opus (main)

| Step | Action | Pass Criteria |
|------|--------|---------------|
| 23.1 | `docker stop njord-e2e` | Container stopped |
| 23.2 | Wait 10s, then `GET /api/states/binary_sensor.forecast_stream` | State = "off" (stream lost) |
| 23.3 | Verify `weather.lucerne_icon_d2` shows "unavailable" or stale state | Reflects downtime |
| 23.4 | `docker start njord-e2e` | Container starts |
| 23.5 | Poll `http://localhost:8080/alive` until HTTP 200 (timeout: 60s) | njord healthy again |
| 23.6 | Poll `GET /api/states/binary_sensor.forecast_stream` until state = "on" (timeout: 120s) | gRPC stream reconnected |
| 23.7 | Verify `weather.lucerne_icon_d2` state ≠ "unavailable" | Entity recovered |

---

## S24 — Teardown: Integration Removal

**Agent:** Opus (main), using claude-in-chrome

| Step | Action | Pass Criteria |
|------|--------|---------------|
| 24.1 | Navigate to Settings → Devices & Services | Integrations page loads |
| 24.2 | Find njord integration, click "Delete" / remove | Deletion confirmed |
| 24.3 | Poll `GET /api/states` until no njord entities remain (interval: 5s, timeout: 30s) | All njord entities gone |
| 24.4 | Verify 0 entities match `njord` device identifiers | No orphans |
| 24.5 | `docker compose -f e2e/docker-compose.e2e.yml down -v` | Stack torn down |

---

## Orchestration

```
┌──────────────────────────────────────────────────────────────────┐
│  Opus Main Agent                                                  │
│  S0: Docker up, health poll                                 [SEQ] │
│  S1: Browser — onboarding, token                            [SEQ] │
│  S2: Browser — integration setup                            [SEQ] │
│  S3: Entity registration polling                            [SEQ] │
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
Verify all WeatherService RPCs (GetCatalog, GetForecast, GetEnrichments, StreamForecasts, StreamEnrichments) and deep entity attribute validation. Use grpcurl for gRPC calls and curl + HA REST API for entity checks. Report ~42 steps.

**Haiku #2 — Connectivity + Ops + Errors (S8–S11):**
Verify connectivity/server entities via HA REST API, all OpsService RPCs (GetStatus, GetTargets), and error handling (invalid gRPC requests, HA REST errors). Report ~27 steps.

**Haiku #3 — HA Browser (S12–S14):**
Verify entity display in HA UI using claude-in-chrome. Navigate Developer Tools → States, filter and inspect weather, enrichment, and server entities. Report ~8 steps.

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

Applied in S19, restored in S21.

```bash
# Apply
grpcurl -plaintext -d '{"alerts":{"enabled":false}}' localhost:8081 njord.v2.AdminService/SetEnrichment
grpcurl -plaintext -d '{}' localhost:8081 njord.v2.OpsService/TriggerPoll

# Restore
grpcurl -plaintext -d '{"alerts":{"enabled":true}}' localhost:8081 njord.v2.AdminService/SetEnrichment
grpcurl -plaintext -d '{}' localhost:8081 njord.v2.OpsService/TriggerPoll
```

- **Expected change:** -15 entities (14 alert sensors + 1 weather_alert event)

### Changed-Horizons Recipe

Applied in S22, restored at end of S22.

```bash
# Apply
grpcurl -plaintext -d '{"horizons":[6,24]}' localhost:8081 njord.v2.AdminService/SetSettings
grpcurl -plaintext -d '{}' localhost:8081 njord.v2.OpsService/TriggerPoll

# Restore
grpcurl -plaintext -d '{"horizons":[3,6,12,24,48,72]}' localhost:8081 njord.v2.AdminService/SetSettings
grpcurl -plaintext -d '{}' localhost:8081 njord.v2.OpsService/TriggerPoll
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
| Stack startup (0.2 → 0.4) | ~9s | njord instant, HA ~5s |
| Integration → entities (2.5 → 3.1) | < 1s | gRPC fast |
| Poll cycle (TriggerPoll → last_updated) | ~30–60s | Depends on Open-Meteo response time |
| Config mutation settle (SetEnrichment → entities update) | ~30–60s | Entity registration after config change |
| Restart → stream reconnect (23.4 → 23.6) | ~12s | forecast_stream off → on |
| Teardown → entities gone (24.2 → 24.3) | < 5s | HA removes entities immediately |
| Full run duration | ~15–20 min | Including multi-cycle waits |
