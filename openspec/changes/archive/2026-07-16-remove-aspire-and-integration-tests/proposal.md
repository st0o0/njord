## Why

The Aspire AppHost and integration test projects add Docker-dependent complexity that blocks running njord in lightweight dev scenarios (no Docker, gRPC-only mode). With `Mqtt:Enabled` now optional, the primary dev loop is `dotnet run` + unit tests — the Aspire orchestrator and container-based integration tests are unnecessary overhead. Removing them now simplifies the solution; they can be rebuilt from scratch later with a cleaner design.

## What Changes

- **Delete** `Njord.AppHost/` project (Aspire orchestrator, mosquitto.conf).
- **Delete** `Njord.ServiceDefaults/` project. Inline its two methods (`AddNjordTelemetry`, `MapDefaultEndpoints`) into the main `Njord/` project.
- **Delete** `Njord.Tests.Integration/` project (Aspire-based container integration tests).
- **Delete** `Njord.Tests.Integration.E2E/` project (E2E pipeline test).
- **Update** `Njord.slnx` — remove all four projects.
- **Update** `Njord.csproj` — remove `ProjectReference` to `ServiceDefaults`.
- **Update** `Program.cs` and `NjordApplicationSetup.cs` — replace `ServiceDefaults` references with local equivalents.
- **Update** `Directory.Packages.props` — remove Aspire packages (`Aspire.Hosting.PostgreSQL`, `Aspire.Hosting.Testing`).
- **Update** `CLAUDE.md` — remove integration/E2E test commands.

## Non-goals

- Rebuilding integration tests with a different approach — that's a future change.
- Removing docker-compose.example.yml — it's for production deployment, not Aspire.
- Removing the `Dockerfile` if one exists — it's for production.
- No API budget impact — this change does not alter polling behavior.

## Capabilities

### New Capabilities

(none)

### Modified Capabilities

- `aspire-apphost`: Entire spec to be removed — AppHost project deleted.
- `aspire-test-fixture`: Entire spec to be removed — test fixture deleted.
- `integration-test-infrastructure`: Entire spec to be removed — integration test projects deleted.
- `test-project-structure`: Solution structure changes (4 projects removed).
- `service-configuration`: `ServiceDefaults` extension methods inlined into the main project.

## Impact

- **Solution**: 4 projects removed from `Njord.slnx`.
- **Build**: faster `dotnet build` (fewer projects, no Aspire SDK).
- **Tests**: unit tests unaffected (501 tests in `Njord.Tests`). Integration/E2E tests deleted (7+1 tests).
- **Dev workflow**: `dotnet run --project Njord/Njord.csproj` works standalone without Docker.
