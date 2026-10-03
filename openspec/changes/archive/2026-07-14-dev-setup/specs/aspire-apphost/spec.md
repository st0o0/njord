## ADDED Requirements

### Requirement: Aspire AppHost project exists
The solution SHALL contain an Aspire AppHost project at `src/Njord.AppHost/` using `Aspire.AppHost.Sdk/13.0.0`. The project SHALL be included in `Njord.slnx` and SHALL reference the `Njord` service project.

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

### Requirement: MQTT Explorer container
The AppHost SHALL start an MQTT Explorer container (`smeagolworms4/mqtt-explorer`) with an HTTP endpoint exposed for browser access. The explorer SHALL be pre-configured to connect to the Mosquitto broker via environment variables.

#### Scenario: MQTT Explorer is accessible
- **WHEN** the AppHost is running
- **THEN** MQTT Explorer is accessible via HTTP in a browser and shows the Mosquitto broker's topic tree

#### Scenario: MQTT Explorer auto-connects to Mosquitto
- **WHEN** MQTT Explorer starts
- **THEN** it connects to the Mosquitto broker without manual configuration

### Requirement: njord project orchestration
The AppHost SHALL run njord as a project reference with `Njord__Mqtt__Host` and `Njord__Mqtt__Port` injected from the Mosquitto container's endpoint. njord SHALL wait for the Mosquitto container before starting.

#### Scenario: njord receives MQTT config from Aspire
- **WHEN** the AppHost starts njord
- **THEN** njord's MQTT configuration points to the Aspire-managed Mosquitto broker
- **AND** njord starts successfully and connects to the broker

#### Scenario: njord waits for Mosquitto
- **WHEN** the AppHost starts
- **THEN** njord does not attempt to start before the Mosquitto container is ready

### Requirement: SQLite launch profile (default)
The AppHost SHALL provide a `sqlite` launch profile that starts njord with SQLite persistence. This SHALL be the default profile. No PostgreSQL container is started.

#### Scenario: Default profile uses SQLite
- **WHEN** the AppHost is launched without specifying a profile
- **THEN** njord uses SQLite persistence at `data/njord-journal.db`
- **AND** no PostgreSQL container is started

### Requirement: PostgreSQL launch profile
The AppHost SHALL provide a `postgres` launch profile that starts a PostgreSQL container and injects `Njord__Persistence__Provider=PostgreSql` and the connection string into njord. njord SHALL wait for the PostgreSQL container.

#### Scenario: Postgres profile starts database
- **WHEN** the AppHost is launched with the `postgres` profile
- **THEN** a PostgreSQL container is started
- **AND** njord receives the PostgreSQL connection string
- **AND** njord uses PostgreSQL as its persistence provider

#### Scenario: Postgres profile does not start in sqlite mode
- **WHEN** the AppHost is launched with the `sqlite` profile
- **THEN** no PostgreSQL container is started

### Requirement: Aspire packages use Central Package Management
All Aspire NuGet package versions SHALL be declared in `src/Directory.Packages.props`, not in the AppHost csproj. The AppHost csproj SHALL contain only `PackageReference` entries without `Version` attributes.

#### Scenario: Package versions in Directory.Packages.props
- **WHEN** the AppHost csproj is inspected
- **THEN** no `PackageReference` has a `Version` attribute
- **AND** the corresponding versions exist in `Directory.Packages.props`
