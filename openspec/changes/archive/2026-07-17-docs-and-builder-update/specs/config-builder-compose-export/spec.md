## ADDED Requirements

### Requirement: docker-compose.yml export format
The Config Builder SHALL offer "docker-compose.yml" as a third export format alongside "appsettings.json" and "Environment Variables". The generated compose file SHALL be a valid docker-compose.yml that can be used directly with `docker compose up -d`.

#### Scenario: Compose export with minimal config
- **WHEN** the user has configured one location "home" (lat 47.05, lon 8.31) with model "icon_eu" and MQTT host "192.168.1.100"
- **THEN** the docker-compose.yml export SHALL contain:
  - `services.njord.image: ghcr.io/st0o0/njord:latest`
  - `services.njord.restart: unless-stopped`
  - `services.njord.volumes` including `njord-data:/app/data`
  - `services.njord.environment` with all configuration as `Njord__*` env vars
  - A top-level `volumes.njord-data` declaration

#### Scenario: Compose export includes enrichment config
- **WHEN** the user has enabled Trends enrichment
- **THEN** the compose environment section SHALL include `Njord__Enrichment__Trends__Enabled=true`

#### Scenario: Compose export with MQTT credentials
- **WHEN** the user has set MQTT username and password
- **THEN** the compose environment SHALL include both `Njord__Mqtt__Username` and `Njord__Mqtt__Password`

#### Scenario: No version key in compose file
- **WHEN** the compose file is generated
- **THEN** it SHALL NOT include a top-level `version:` key (modern Compose format)

### Requirement: Copy compose export
The Config Builder's copy button SHALL work with the docker-compose.yml format, copying the full YAML content to the clipboard.

#### Scenario: Copy compose to clipboard
- **WHEN** the user selects docker-compose.yml format and clicks "Copy"
- **THEN** the clipboard SHALL contain the full docker-compose.yml content
- **AND** the button text SHALL briefly change to "Copied!"
