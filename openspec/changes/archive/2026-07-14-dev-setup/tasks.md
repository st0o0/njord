## 1. Docker Compose Reference

- [x] 1.1 Rename `docker-compose.yml` → `docker-compose.example.yml` with fully-commented environment variables for all options from `NjordOptions`, `MqttOptions`, `PersistenceOptions`, `EnrichmentOptions`, `ParameterOptions`, `RequestBudget`. Required values uncommented, optional commented with defaults. Sections separated by comment headers. Named volume `njord-data` for `/app/data`. Two location entries (one active, one commented).

## 2. Aspire AppHost Project Setup

- [x] 2.1 Create `src/Njord.AppHost/Njord.AppHost.csproj` with `Aspire.AppHost.Sdk/13.0.0`, project reference to `../Njord/Njord.csproj`, and `PackageReference` to `Aspire.Hosting.PostgreSQL` (no version — CPM).
- [x] 2.2 Add `Aspire.Hosting.PostgreSQL` version `13.4.6` to `src/Directory.Packages.props`.
- [x] 2.3 Add `Njord.AppHost` to `src/Njord.slnx`.
- [x] 2.4 Create `src/Njord.AppHost/mosquitto.conf` with `listener 1883` and `allow_anonymous true`.

## 3. AppHost Orchestration

- [x] 3.1 Create `src/Njord.AppHost/Program.cs`: Mosquitto container (`eclipse-mosquitto:2`) with endpoint on 1883/tcp, bind-mount of `mosquitto.conf`, and `WithPersistentLifetime()`.
- [x] 3.2 Add MQTT Explorer container (`smeagolworms4/mqtt-explorer`) with HTTP endpoint, env-var wiring to Mosquitto host/port, and `WaitFor(mosquitto)`.
- [x] 3.3 Add PostgreSQL toggle: read `UsePostgres` from configuration; when true, `AddPostgres("pg").AddDatabase("njord-db")` with `WithPersistentLifetime()`.
- [x] 3.4 Add njord project reference: `AddProject<Projects.Njord>("njord")` with `Njord__Mqtt__Host`/`Port` from Mosquitto endpoint, `WaitFor(mosquitto)`. When PostgreSQL is active, inject `Njord__Persistence__Provider=PostgreSql` and the connection string, and `WaitFor(postgres)`.

## 4. Launch Profiles

- [x] 4.1 Create `src/Njord.AppHost/Properties/launchSettings.json` with two profiles: `sqlite` (default, `UsePostgres=false`) and `postgres` (`UsePostgres=true`). Both set `DOTNET_ENVIRONMENT=Development` and `ASPIRE_DASHBOARD_OTLP_ENDPOINT_URL`.

## 5. Validation

- [x] 5.1 Run `dotnet build Njord.slnx` from `src/` — verify the AppHost compiles and the solution builds cleanly.
- [x] 5.2 Run existing tests: `dotnet run --project Njord.Tests/Njord.Tests.csproj` — verify no regressions.
