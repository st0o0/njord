# E2E Test Plan — njord + ha-njord

Agent-orchestrated end-to-end test against a real Docker stack.
gRPC + REST. Real Open-Meteo API. 6 domains (Forecast, Ensemble, AirQuality, Flood, Marine, Seasonal),
30 alert types, 23 observation sensors, lifestyle features, multi-model weather comparison.

## Section Index

| Section | Name | Steps | Agent |
|---------|------|-------|-------|
| S0 | Stack Setup | 4 | Opus |
| S1 | HA Onboarding + Token | 6 | Opus |
| S2 | Integration Setup | 3 | Opus |
| S3 | Entity Registration | 6 | Opus |
| S4 | Domain Data: Weather | 5 | Haiku #1 |
| S5 | Domain Data: Air Quality | 4 | Haiku #1 |
| S6 | Domain Data: Flood | 3 | Haiku #1 |
| S7 | Alert Binary Sensors | 4 | Haiku #2 |
| S7b | Score Sensors | 4 | Haiku #2 |
| S7c | Condition Sensors | 6 | Haiku #2 |
| S8 | gRPC: GetManifest + GetStatus | 2 | Haiku #2 |
| S9 | gRPC: Subscribe | 2 | Haiku #2 |
| S10 | gRPC: GetStatus + TriggerPoll | 5 | Haiku #3 |
| S11 | Server + Diagnostics Entities | 4 | Haiku #3 |
| S12 | Health Endpoints | 2 | Haiku #3 |
| S13 | Multi-Cycle: TriggerPoll × 2 | 4 | Opus |
| S14 | Budget Tracking | 3 | Opus |
| S15 | Teardown | 3 | Opus |
| | **Total** | **~70** | |

---

## Stack

| Service | Image | Ports |
|---------|-------|-------|
| njord | Built from Dockerfile | 8080 (HTTP), 8081 (gRPC) |
| homeassistant | HA stable | 8123 |

### Configuration

- **Location:** Barcelona (explicit coordinates 41.3874 / 2.1686 in the compose file, coastal for full Marine coverage)
- **Weather:** ForecastDays=4, 3 ensemble models (`ecmwf_ifs025_ensemble_mean`, `ecmwf_aifs025_ensemble_mean`, `ukmo_global_ensemble_mean_20km`)
- **Features:** ALL enabled (87 feature toggles: 64 regular + 23 observations; `HeatPumpCop` with `TargetTemperature=35`)
- **Resolved domains:** Forecast, AirQuality, Marine, Seasonal (Flood does not resolve for coastal Barcelona)

### Expected Entities

Derived from config: 1 location (Barcelona), all 87 features enabled, 3 ensemble models.
Actual count is data-dependent: features that return null produce no entity.

| Platform | Count | Pattern | Notes |
|----------|-------|---------|-------|
| weather | 4 | `weather.barcelona` + 3 model entities | 1 primary (best_match) + 1 per ensemble model |
| sensor (observations) | ~23 | `barcelona_*_observation` | One per observation feature; AQ, marine, flood, forecast observations |
| sensor (lifestyle) | ~30 | `barcelona_commute`, `barcelona_exercise`, `barcelona_bbq`, `barcelona_laundry`, `barcelona_outdoor`, `barcelona_solar_yield`, `barcelona_surf`, `barcelona_ventilation`, `barcelona_night_ventilation`, `barcelona_gardening`, `barcelona_confidence_trend`, `barcelona_front_approaching`, `barcelona_pollen_trend`, `barcelona_ragweed_pollen`, `barcelona_granular_aqi`, `barcelona_elevated_wind`, `barcelona_ocean_currents`, `barcelona_snow_depth_watch`, `barcelona_soil_moisture`, `barcelona_evapotranspiration`, `barcelona_growing_degree`, `barcelona_flood_timeline`, `barcelona_ensemble_model_comparison`, `barcelona_forecast_model_comparison`, `barcelona_seasonal_outlook`, ... | Data-dependent; some produce null and no entity appears |
| sensor (scores) | 4 | `barcelona_allergy_severity`, `barcelona_sleep_quality`, `barcelona_asthma_risk`, `barcelona_spray_window` | Score sensors 0-100 |
| sensor (conditions) | 6 | `barcelona_heat_pump_cop`, `barcelona_irrigation_need`, `barcelona_frost_depth`, `barcelona_inversion_detection`, `barcelona_atmospheric_stability`, `barcelona_cloud_ceiling` | Condition sensors with typed values in `extra` map |
| sensor (server) | 4 | `server_monthly_usage`, `server_daily_usage`, `server_version`, `server_uptime` | Global diagnostics |
| binary_sensor (alerts) | 30 | `barcelona_{frost,heat,storm,...}` | No `_alert` suffix. Incl. `compound_heat_stress`, `migraine_risk`, `fire_risk`, `coastal_flood`, `pollen_blast`, `smog_risk`, `shower` |
| button | 1 | `button.server_trigger_poll` | Global manual poll trigger |
| **Total** | **~97** | | Updated 2026-11-10. Entity count is data-dependent. Flood does not resolve for coastal Barcelona. |

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
2. Count entities matching njord platform: expect ~97 (data-dependent)
3. Verify `weather.barcelona` exists with state in HA condition list
4. Verify at least 1 observation sensor exists (pattern: `sensor.barcelona_*_observation`)
5. Verify at least 1 alert binary_sensor exists (pattern: `binary_sensor.barcelona_*`)
6. Verify `button.server_trigger_poll` exists

---

## Parallel Block — Spawn 3 Haiku Subagents

After S3 completes, spawn three subagents in a single message.

---

## S4 — Domain Data: Weather (Haiku #1)

1. `weather.barcelona` has `temperature` attribute (numeric)
2. `weather.barcelona` has `humidity` attribute (numeric, 0-100)
3. `weather.barcelona` has `wind_speed` attribute (numeric, ≥ 0)
4. `POST /api/services/weather/get_forecasts?return_response` with `{"entity_id":"weather.barcelona","type":"hourly"}` → `service_response["weather.barcelona"].forecast` is a non-empty list (daily is unsupported)
5. `weather.barcelona` state is a valid HA weather condition string

---

## S5 — Domain Data: Air Quality (Haiku #1)

1. `sensor.barcelona_pm25_observation` has numeric state >= 0
2. `sensor.barcelona_ozone_observation` has numeric state >= 0
3. `sensor.barcelona_pm10_observation` has numeric state >= 0 (enabled in compose)
4. AQ observation sensors have `unit_of_measurement` attribute

---

## S6 — Domain Data: Flood (Haiku #1)

1. `sensor.barcelona_flood_discharge_observation` — Barcelona is coastal, Flood domain may not resolve; SKIP if entity not found or unavailable
2. If present and available: numeric state >= 0 with `unit_of_measurement` attribute
3. If present and available: discharge value is plausible (0-10000 m3/s)

---

## S7 — Alert Binary Sensors (Haiku #2)

1. 30 alert binary_sensors exist (named `binary_sensor.barcelona_<feature>`, no `_alert` suffix)
2. Each has `state` in {"on", "off", "unavailable"} (unavailable = not yet evaluated or domain not resolved)
3. At least these specific alerts exist: `frost`, `heat`, `storm`, `uv`, `pollen`, `compound_heat_stress`, `migraine_risk`, `fire_risk`, `coastal_flood`, `smog_risk`, `pollen_blast`, `shower`
4. At least 1 alert is "on" (triggered) when weather data is available

---

## S7b — Score Sensors (Haiku #2)

1. `sensor.*_allergy_severity` has numeric state 0-100
2. `sensor.*_sleep_quality` has numeric state 0-100
3. `sensor.*_asthma_risk` has numeric state 0-100
4. `sensor.*_spray_window` has numeric state 0-100

---

## S7c — Condition Sensors (Haiku #2)

Conditions are binary ("on"/"off") in HA with typed values in the `extra` attributes map.

1. `sensor.*_heat_pump_cop` state in {"on", "off", "unavailable"}; attributes include `cop` (numeric > 0)
2. `sensor.*_irrigation_need` state in {"on", "off", "unavailable"}; attributes include `advice` (water/skip/reduce/soil_wet)
3. `sensor.*_frost_depth` state in {"on", "off", "unavailable"}; attributes include `depth_cm` (numeric >= 0)
4. `sensor.*_inversion_detection` state in {"on", "off", "unavailable"}; attributes include `strength` (none/mild/strong/extreme)
5. `sensor.*_atmospheric_stability` state in {"on", "off", "unavailable"}; attributes include `stability_class` (stable/marginal/capped/unstable/explosive)
6. `sensor.*_cloud_ceiling` state in {"on", "off", "unavailable"}; attributes include `ceiling_m` (numeric >= 0)

---

## S8 — gRPC: GetManifest + GetStatus (Haiku #2)

Using `grpcurl` with `-plaintext -import-path protos -proto njord/njord.proto localhost:8081 njord.NjordService/<RPC>`:

1. `GetManifest` returns entity definitions; verify Barcelona location present with alert, score, condition, observation entities
2. `GetStatus` returns budget info: `monthlyUsed` >= 0, `monthlyLimit` > 0, `activeEnrichments` list non-empty

---

## S9 — gRPC: Subscribe (Haiku #2)

1. `Subscribe` with `-max-time 3` returns entity state snapshot (scores, conditions, alerts, observations)
2. Snapshot contains at least 10 entity states (grpcurl exits with code 68 DeadlineExceeded, which is expected)

---

## S10 — gRPC: GetStatus + TriggerPoll (Haiku #3)

Using `grpcurl` with `-plaintext -import-path protos -proto njord/njord.proto localhost:8081 njord.NjordService/<RPC>`:

1. `GetManifest` returns at least 1 location named "Barcelona" with entity definitions
2. `GetStatus` -> `budget` has `monthlyUsed` >= 0 and `monthlyLimit` > 0
3. `GetStatus` -> `activeEnrichments` count >= 80 (all features enabled); poll state is reported per location
4. `TriggerPoll` with `{}` -> `triggeredCount` >= 1
5. After trigger: `GetStatus` shows incremented `monthlyUsed`

---

## S11 — Server + Diagnostics Entities (Haiku #3)

1. `sensor.server_monthly_usage` has numeric state (a percentage, `suggested_display_precision=1`; rounds to 0.0 early in the test since 23/300000 = 0.008%)
2. `sensor.server_version` state has a non-empty state (`0.0.0-dev` in local builds)
3. `sensor.server_uptime` has numeric state (hours, `suggested_display_precision=1`; rounds to 0.0 for the first ~6 minutes)
4. `button.server_trigger_poll` exists

---

## S12 — Health Endpoints (Haiku #3)

1. `GET /healthz` -> 200 with plain text `Healthy` or `Degraded`
2. `GET /alive` -> 200

---

## S13 — Multi-Cycle: TriggerPoll × 2 (Opus)

1. Record initial `monthly_used` from `GetStatus`
2. `TriggerPoll` location "Barcelona" → `triggeredCount` ≥ 1
3. Wait 30s for poll round to complete
4. Verify `monthly_used` increased (expect increment ~12, sum of call weights of resolved domains)

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
