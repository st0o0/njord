# E2E Test Plan — njord + ha-njord

Agent-orchestrated end-to-end test against a real Docker stack.
gRPC + REST. Real Open-Meteo API. 6 domains (Weather, AirQuality, Flood, Marine, Seasonal, Ensemble),
30 alert types, lifestyle features, multi-model weather comparison.

## Section Index

| Section | Name | Steps | Agent |
|---------|------|-------|-------|
| S0 | Stack Setup | 4 | Opus |
| S1 | HA Onboarding + Token | 6 | Opus |
| S2 | Integration Setup | 3 | Opus |
| S3 | Entity Registration | 8 | Opus |
| S4 | Domain Data: Weather | 5 | Haiku #1 |
| S5 | Domain Data: Air Quality | 4 | Haiku #1 |
| S6 | Domain Data: Flood | 3 | Haiku #1 |
| S7 | Alert Binary Sensors | 5 | Haiku #2 |
| S7b | Score Sensors | 4 | Haiku #2 |
| S7c | Condition Sensors | 6 | Haiku #2 |
| S7d | Calendar Entity | 2 | Haiku #2 |
| S7e | Event Entities | 2 | Haiku #2 |
| S8 | gRPC: GetDomainData + StreamDomainData | 5 | Haiku #2 |
| S9 | gRPC: GetEnrichments + StreamEnrichments | 4 | Haiku #2 |
| S10 | gRPC: GetCatalog + GetStatus + TriggerPoll | 5 | Haiku #3 |
| S11 | Server + Diagnostics Entities | 4 | Haiku #3 |
| S12 | Error Handling | 3 | Haiku #3 |
| S13 | Multi-Cycle: TriggerPoll × 2 | 4 | Opus |
| S14 | Budget Tracking | 3 | Opus |
| S15 | Teardown | 3 | Opus |
| | **Total** | **~80** | |

---

## Stack

| Service | Image | Ports |
|---------|-------|-------|
| njord | Built from Dockerfile | 8080 (HTTP), 8081 (gRPC) |
| homeassistant | HA stable | 8123 |

### Configuration

- **Location:** Barcelona (explicit coordinates 41.3874 / 2.1686 in the compose file, coastal for full Marine coverage)
- **Weather:** ForecastDays=4, models `ecmwf_ifs025_ensemble_mean` (primary) + `ecmwf_aifs025_ensemble_mean`
  (`EnsembleModelComparison` default-on with 2+ models)
- **Features:** ALL enabled (67 feature toggles: 64 explicit + 3 default-on; `HeatPumpCop` with `TargetTemperature=35`)
- **Resolved domains:** Weather, AirQuality, Marine, Seasonal (Flood does not resolve for coastal Barcelona)

### Expected Entities

Derived from config: 1 location (Barcelona), all 67 features enabled, 3 ensemble models.

| Platform | Count | Pattern | Notes |
|----------|-------|---------|-------|
| weather | 4 | `weather.barcelona` + 3 model entities | 1 primary (best_match) + 1 per ensemble model |
| sensor (AQ) | 7 | `barcelona_pm2_5`, `barcelona_pm10`, `barcelona_ozone`, `barcelona_birch_pollen`, `barcelona_grass_pollen`, `barcelona_alder_pollen`, `barcelona_mugwort_pollen` | Air quality |
| sensor (marine) | 4 | `barcelona_wave_height`, `barcelona_wave_period`, `barcelona_wave_direction`, `barcelona_water_temperature` | Marine (coastal only) |
| sensor (lifestyle) | 26 | `barcelona_commute_safety`, `barcelona_exercise`, `barcelona_ensemble_model_spread`, `barcelona_seasonal_temperature_anomaly`, `barcelona_solar_yield`, `barcelona_bbq_score`, `barcelona_laundry_score`, `barcelona_outdoor_score`, `barcelona_surf_conditions`, `barcelona_wind_speed_80_m`, `barcelona_ragweed_pollen_peak`, `barcelona_cumulative_growing_degree_days`, `barcelona_evapotranspiration`, `barcelona_soil_moisture`, `barcelona_granular_aqi`, `barcelona_ocean_current_velocity`, ... | Data-dependent; some features produce null and no entity appears |
| sensor (scores) | 4 | `barcelona_allergy_severity`, `barcelona_sleep_quality`, `barcelona_asthma_risk`, `barcelona_spray_window` | Score sensors 0-100 |
| sensor (conditions) | 6 | `barcelona_heat_pump_cop`, `barcelona_irrigation_need`, `barcelona_frost_depth`, `barcelona_inversion_detection`, `barcelona_atmospheric_stability`, `barcelona_cloud_ceiling` | Numeric and enum condition sensors |
| sensor (server) | 4 | `server_monthly_usage`, `server_daily_usage`, `server_version`, `server_uptime` | Global diagnostics |
| binary_sensor (alerts) | 30 | `barcelona_{frost,heat,storm,...}_alert` | All alert features incl. `compound_heat_stress`, `migraine_risk`, `fire_risk`, `coastal_flood`, `pollen_blast`, `smog_risk`, `shower` |
| binary_sensor (lifestyle) | 2 | `barcelona_planting_safe`, `barcelona_ventilation_window` | Condition binary sensors |
| binary_sensor (streams) | 2 | `server_domain_stream`, `server_enrichment_stream` | Global connectivity |
| calendar | 1 | `calendar.barcelona_activity_windows` | Activity windows |
| event | 3 | `event.barcelona_weather_alert`, `event.barcelona_data`, `event.barcelona_features` | Alert, data lifecycle, feature transitions |
| button | 1 | `button.server_trigger_poll` | Global manual poll trigger |
| **Total** | **84** | | Confirmed 2026-11-01. Entity ids follow HA device + name naming. Flood does not resolve for coastal Barcelona. |

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
2. Count entities matching `njord` platform: expect ~84
3. Verify `weather.barcelona` exists with state ∈ HA condition list
4. Verify at least 1 AQ sensor exists (pattern: `sensor.barcelona_pm*`)
5. Verify at least 1 alert binary_sensor exists (pattern: `binary_sensor.barcelona_*_alert`)
6. Verify `calendar.*_activity_windows` exists
7. Verify `event.*_data` exists (event_types: domain_updated, features_updated)
8. Verify `event.*_features` exists (event_types: score_threshold, condition_changed, window_opened, window_closing_soon, window_closed)

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

1. `sensor.barcelona_pm2_5` has numeric state ≥ 0
2. `sensor.barcelona_ozone` has numeric state ≥ 0
3. At least one pollen sensor has numeric state (birch, grass, alder or mugwort)
4. AQ sensors have `unit_of_measurement` attribute

---

## S6 — Domain Data: Flood (Haiku #1)

1. `sensor.barcelona_river_discharge` — Barcelona is coastal, Flood domain may not resolve; SKIP if entity not found
2. If present: numeric state ≥ 0 with `unit_of_measurement` attribute
3. If present: discharge value is plausible (0-10000 m³/s)

---

## S7 — Alert Binary Sensors (Haiku #2)

1. 30 alert binary_sensors exist matching `*_alert`
2. Each has `state` ∈ {"on", "off"}
3. At least one alert from each category exists: frost, heat, storm, uv, pollen; the newer `compound_heat_stress`, `migraine_risk`, `fire_risk`, `coastal_flood`, `smog_risk`, `pollen_blast`, `shower` binary_sensors exist
4. `event.barcelona_weather_alert` exists with `event_types`: `alert_started`, `alert_escalated`, `alert_deescalated`, `alert_cleared`
5. Alert severity is delivered via the event entity data (not as a binary_sensor attribute)

---

## S7b — Score Sensors (Haiku #2)

1. `sensor.*_allergy_severity` has numeric state 0-100
2. `sensor.*_sleep_quality` has numeric state 0-100
3. `sensor.*_asthma_risk` has numeric state 0-100
4. `sensor.*_spray_window` has numeric state 0-100

---

## S7c — Condition Sensors (Haiku #2)

1. `sensor.*_heat_pump_cop` has numeric state (COP value, > 0)
2. `sensor.*_irrigation_need` state ∈ {water, skip, reduce, soil_wet}
3. `sensor.*_frost_depth` has numeric state (cm, >= 0)
4. `sensor.*_inversion_detection` state ∈ {none, mild, strong, extreme}
5. `sensor.*_atmospheric_stability` state ∈ {stable, marginal, capped, unstable, explosive}
6. `sensor.*_cloud_ceiling` has numeric state (meters, >= 0)

---

## S7d — Calendar Entity (Haiku #2)

1. `calendar.*_activity_windows` exists
2. It has events (the list may be empty if no windows are computed yet)

---

## S7e — Event Entities (Haiku #2)

1. `event.*_data` exists with `event_types` domain_updated, features_updated
2. `event.*_features` exists with `event_types` score_threshold, condition_changed, window_opened, window_closing_soon, window_closed

---

## S8 — gRPC: GetDomainData + StreamDomainData (Haiku #2)

Using `grpcurl` with `-import-path protos -proto njord/v2/weather.proto`:

1. `GetDomainData` with location "Barcelona" and domain `DOMAIN_FORECAST` (domain is required) → response has the oneof `forecast` non-empty
2. `GetDomainData` with domain filter `DOMAIN_AIR_QUALITY` → response has the oneof `airQuality`
3. Open `StreamDomainData` (no TriggerPoll needed) — the stream replays cached domain data immediately via `Source.From(snapshot).Concat(liveHub)`. Expect data within seconds.
4. Domain data update contains `location` field matching "Barcelona"
5. Weather data has `temperature`, `wind_speed`, `precipitation` fields (`precipitation` may be omitted when 0)
6. Note: Marine returns NotFound for inland locations by design

---

## S9 — gRPC: GetEnrichments + StreamEnrichments (Haiku #2)

1. `GetEnrichments` with location "Barcelona" → has an `events[]` list
2. `events[]` has at least 1 entry; the feature is the oneof case (e.g. `fog`) and the alert severity is at `<case>.alert.severity`
3. Open `StreamEnrichments` (no TriggerPoll needed) — the stream replays cached enrichment events immediately. Expect data within seconds.
4. Event has a feature oneof case set (`type_name` no longer exists)
5. Enrichment events of the new features (e.g. `compound_heat_stress`, `allergy_severity`, `heat_pump_cop`, `cloud_ceiling`) appear in the stream

---

## S10 — gRPC: GetCatalog + GetStatus + TriggerPoll (Haiku #3)

1. `GetCatalog` on `njord.v2.WeatherService` → has at least 1 location named "Barcelona" and lists weather models
2. `GetStatus` → `budget` has `monthly_used` ≥ 0 and `monthly_limit` > 0
3. `GetStatus` → `activeEnrichments` count = 65 (all features enabled); poll state is reported per location
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
2. `TriggerPoll` location "Barcelona" → `triggeredCount` ≥ 1
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
