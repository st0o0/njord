## ADDED Requirements

### Requirement: Docker Compose E2E stack definition

`e2e/docker-compose.e2e.yml` SHALL define a two-service stack: njord (built from the repo's Dockerfile) and Home Assistant (official stable image with ha-njord mounted).

#### Scenario: njord service
- **WHEN** the stack starts
- **THEN** njord is built from the repo root Dockerfile, exposes ports 8080 (HTTP) and 8081 (gRPC), and is configured with 1 location (lucerne, 47.05/8.31), 2 models (icon_d2 + ecmwf_ifs025), and all 6 enrichments enabled

#### Scenario: Home Assistant service
- **WHEN** the stack starts
- **THEN** HA uses `ghcr.io/home-assistant/home-assistant:stable`, exposes port 8123, and mounts `ha-njord/custom_components/njord` as a read-only volume at `/config/custom_components/njord`

#### Scenario: ha-njord source path
- **WHEN** the compose file references ha-njord
- **THEN** it uses a relative path that assumes ha-njord is cloned as a sibling directory (e.g., `../../ha-njord/custom_components/njord` from `e2e/` inside njord repo)

#### Scenario: No MQTT service
- **WHEN** the stack is defined
- **THEN** there is no Mosquitto or MQTT broker service — the stack tests the gRPC path exclusively

### Requirement: Clean-slate execution

Each E2E run SHALL start from a clean state with no persisted data from previous runs.

#### Scenario: Volume cleanup
- **WHEN** the E2E test starts
- **THEN** `docker compose down -v` removes all named volumes before `docker compose up -d --build`

### Requirement: Minimal but fully instrumented config

The njord configuration in the E2E stack SHALL be minimal (1 location, 2 models) but enable all enrichment features.

#### Scenario: Enrichment features
- **WHEN** njord starts in the E2E stack
- **THEN** consensus, alerts, derived, trends, indices, and history enrichments are all enabled

#### Scenario: Real Open-Meteo
- **WHEN** njord polls for forecast data
- **THEN** it hits the real Open-Meteo API (no mock server), consuming approximately 2 API requests per poll cycle
