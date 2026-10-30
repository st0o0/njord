## Why

The Akka bootstrap in Njord has diverged from the FunkArr reference pattern with unnecessary abstractions (PersistenceSelection record, AkkaLogLevel parser, Port-0 self-join) and the telemetry stack uses prometheus-net instead of the OpenTelemetry SDK that FunkArr uses. Aligning both brings consistency across projects and removes code that adds indirection without value.

## What Changes

### Akka Bootstrap

- Inline persistence selection in AkkaSetupContainer: detect Postgres via `ConnectionString` being set (like FunkArr's Host check), else SQLite at DataPath
- Remove `AkkaLogLevel.cs` -- just `ClearLoggers()` + `AddLoggerFactory()` like FunkArr
- Remove Port-0 self-join logic -- hardcode `localhost:2552` like FunkArr
- **BREAKING**: Remove `PersistenceProvider` enum and `Provider` property from `PersistenceOptions` -- detection is now implicit from `ConnectionString`
- Remove `PersistenceSelection.cs` record class
- Remove `PersistenceOptionsValidator` (validation is now inline)
- Update `HealthSetupContainer` to check `string.IsNullOrEmpty(ConnectionString)` instead of `Provider == Sqlite`

### Telemetry

- Replace prometheus-net + prometheus-net.AspNetCore with OpenTelemetry SDK
- Rewrite `TelemetrySetupContainer` using `AddOpenTelemetry().ConfigureResource().WithMetrics()` with `AddMeter` per Njord meter and `AddPrometheusExporter()`
- Remove `NjordMeters` set from `TelemetrySetupContainer`
- Replace `UseHttpMetrics()`/`MapMetrics()` with `MapPrometheusScrapingEndpoint()` in `NjordApplicationSetup`

## Non-goals

- No changes to `IActorRegistration` pattern or domain setup containers
- No changes to `HttpMetricsHandler` or domain `Telemetry` classes (they write to .NET Meters which OTel collects)
- No changes to actor topology, domain logic, or polling
- No Akka.Management or other Akka infrastructure changes
- No API-budget impact (this change does not alter polling)

## Capabilities

### New Capabilities

(none)

### Modified Capabilities

- `persistence-provider`: Remove `PersistenceProvider` enum; persistence selection becomes implicit from `ConnectionString` presence
- `telemetry-infrastructure`: Replace prometheus-net with OpenTelemetry SDK; same `/metrics` endpoint, different library
- `servus-bootstrap`: AkkaSetupContainer simplified to match FunkArr reference pattern

## Impact

- **Config**: `Njord:Persistence:Provider` setting removed; users with `Provider=PostgreSql` must set `ConnectionString` instead (which they already do). SQLite users are unaffected.
- **Packages**: Remove 2 NuGet packages (prometheus-net), add 6 (OpenTelemetry.*)
- **Files deleted**: 4 (AkkaLogLevel.cs, PersistenceSelection.cs, PersistenceProvider.cs, PersistenceOptionsValidator.cs)
- **Files rewritten**: 5 (AkkaSetupContainer.cs, PersistenceOptions.cs, TelemetrySetupContainer.cs, NjordApplicationSetup.cs, HealthSetupContainer.cs)
- **Tests**: Update any tests referencing `PersistenceProvider` enum or the removed files
- **docker-compose.example.yml**: Remove `Provider` env var if present
- **Config Builder**: Update if it references Provider
