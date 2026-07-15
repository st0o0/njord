# Capability: aspire-apphost

## Purpose

Aspire AppHost orchestration for test infrastructure -- starts Mosquitto, WireMock, and njord with the correct wiring for integration and E2E tests.

## Requirements

### Requirement: Aspire AppHost project exists
The solution SHALL contain an Aspire AppHost project at `src/Njord.AppHost/` using `Aspire.AppHost.Sdk`. The project SHALL be included in `Njord.slnx` and SHALL reference the `Njord` service project. The AppHost is used exclusively for test infrastructure.

#### Scenario: AppHost builds successfully
- **WHEN** `dotnet build` is run on the AppHost project
- **THEN** the build succeeds without errors

#### Scenario: AppHost is part of the solution
- **WHEN** `dotnet build Njord.slnx` is run from `src/`
- **THEN** the AppHost project is included in the build

### Requirement: Mosquitto broker container
The AppHost SHALL start an Eclipse Mosquitto v2 container with a bind-mounted configuration file that enables anonymous connections on port 1883. The Mosquitto container SHALL use `WithPersistentLifetime()` to survive AppHost restarts.

#### Scenario: Mosquitto accepts connections
- **WHEN** the AppHost is running
- **THEN** a Mosquitto broker is available on port 1883 accepting anonymous MQTT connections

#### Scenario: Mosquitto configuration is explicit
- **WHEN** the Mosquitto container starts
- **THEN** it uses a `mosquitto.conf` file with `listener 1883` and `allow_anonymous true`

### Requirement: njord project orchestration
The AppHost SHALL run njord as a project reference with `Njord__Mqtt__Host` and `Njord__Mqtt__Port` injected from the Mosquitto container's endpoint. njord SHALL wait for the Mosquitto container before starting.

#### Scenario: njord receives MQTT config from Aspire
- **WHEN** the AppHost starts njord
- **THEN** njord's MQTT configuration points to the Aspire-managed Mosquitto broker
- **AND** njord starts successfully and connects to the broker

#### Scenario: njord waits for Mosquitto
- **WHEN** the AppHost starts
- **THEN** njord does not attempt to start before the Mosquitto container is ready

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

### Requirement: Aspire packages use Central Package Management
All Aspire NuGet package versions SHALL be declared in `src/Directory.Packages.props`, not in the AppHost csproj. The AppHost csproj SHALL contain only `PackageReference` entries without `Version` attributes.

#### Scenario: Package versions in Directory.Packages.props
- **WHEN** the AppHost csproj is inspected
- **THEN** no `PackageReference` has a `Version` attribute
- **AND** the corresponding versions exist in `Directory.Packages.props`
