## MODIFIED Requirements

### Requirement: Aspire AppHost project exists
The solution SHALL contain an Aspire AppHost project at `src/Njord.AppHost/` using `Aspire.AppHost.Sdk`. The project SHALL be included in `Njord.slnx` and SHALL reference the `Njord` service project. The AppHost is used exclusively for test infrastructure.

#### Scenario: AppHost builds successfully
- **WHEN** `dotnet build` is run on the AppHost project
- **THEN** the build succeeds without errors

#### Scenario: AppHost is part of the solution
- **WHEN** `dotnet build Njord.slnx` is run from `src/`
- **THEN** the AppHost project is included in the build

## REMOVED Requirements

### Requirement: MQTT Explorer container
**Reason**: AppHost is repurposed for test-only use; MQTT Explorer is a dev debugging tool with no role in automated tests.
**Migration**: Use standalone MQTT Explorer or `mosquitto_sub` for manual debugging.

### Requirement: SQLite launch profile (default)
**Reason**: Launch profiles are irrelevant for test-only AppHost. Tests use SQLite persistence by default without explicit profile selection.
**Migration**: No action needed — SQLite is the implicit default when no PostgreSQL is configured.

### Requirement: PostgreSQL launch profile
**Reason**: Launch profiles are irrelevant for test-only AppHost. PostgreSQL testing is a non-goal of this change.
**Migration**: PostgreSQL testing can be re-added as a separate Aspire test configuration if needed later.

## ADDED Requirements

### Requirement: WireMock container in AppHost
The AppHost SHALL start a WireMock container (`wiremock/wiremock:latest`) with an HTTP endpoint exposed on port 8080. The container SHALL be named `wiremock`.

#### Scenario: WireMock accepts admin API requests
- **WHEN** the AppHost is running
- **THEN** the WireMock container SHALL be accessible via HTTP and respond to `/__admin/mappings` requests

### Requirement: Njord receives WireMock endpoint as OpenMeteoBaseUrl
The AppHost SHALL inject the WireMock container's HTTP endpoint as `Njord__OpenMeteoBaseUrl` into the Njord project. Njord SHALL wait for both Mosquitto and WireMock before starting.

#### Scenario: Njord uses WireMock as API backend
- **WHEN** the AppHost starts Njord
- **THEN** Njord's Open-Meteo client SHALL send requests to the WireMock container instead of `api.open-meteo.com`

#### Scenario: Njord waits for WireMock
- **WHEN** the AppHost starts
- **THEN** Njord does not attempt to start before the WireMock container is ready
