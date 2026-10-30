## 1. Simplify PersistenceOptions

- [x] 1.1 Remove `PersistenceProvider` enum from `src/Njord.Core/Configuration/PersistenceProvider.cs`
- [x] 1.2 Remove `Provider` property from `src/Njord.Core/Configuration/PersistenceOptions.cs`; keep `ConnectionString?` and `DataPath`
- [x] 1.3 Remove `PersistenceOptionsValidator` from `src/Njord.Core/Configuration/PersistenceOptionsValidator.cs` (or wherever it lives)
- [x] 1.4 Update `src/Njord/Configuration/ConfigSetupContainer.cs`: change `AddNjordOptions<PersistenceOptions, PersistenceOptionsValidator>()` to `AddNjordOptions<PersistenceOptions>()`
- [x] 1.5 Update tests in `src/Njord.Core.Tests/` that reference `PersistenceProvider` enum or validator
- [x] 1.6 Update `src/Njord/Configuration/HealthSetupContainer.cs`: replace `persistence.Provider == PersistenceProvider.Sqlite` with `string.IsNullOrEmpty(persistence.ConnectionString)`

## 2. Rewrite AkkaSetupContainer

- [x] 2.1 Delete `src/Njord/Configuration/PersistenceSelection.cs`
- [x] 2.2 Delete `src/Njord/Configuration/AkkaLogLevel.cs`
- [x] 2.3 Rewrite `src/Njord/Configuration/AkkaSetupContainer.cs`: inline persistence selection (ConnectionString → Postgres, else SQLite at DataPath), `ClearLoggers()` + `AddLoggerFactory()`, hardcoded `localhost:2552`, keep `IActorRegistration` loop
- [x] 2.4 Remove `Akka.Cluster` using and self-join startup callback
- [x] 2.5 Update `src/Njord.IntegrationTests/` if any tests reference `ConfigureLogging`, `ConfigureInfrastructure`, `PersistenceSelection`, or `AkkaLogLevel`

## 3. Migrate Telemetry to OpenTelemetry

- [x] 3.1 Remove `prometheus-net` and `prometheus-net.AspNetCore` from `src/Directory.Packages.props`
- [x] 3.2 Add OpenTelemetry packages to `src/Directory.Packages.props`: `OpenTelemetry`, `OpenTelemetry.Extensions.Hosting`, `OpenTelemetry.Instrumentation.AspNetCore`, `OpenTelemetry.Instrumentation.Process`, `OpenTelemetry.Instrumentation.Runtime`, `OpenTelemetry.Exporter.Prometheus.AspNetCore`
- [x] 3.3 Update package references in `src/Njord/Njord.csproj`: replace prometheus-net refs with OpenTelemetry refs
- [x] 3.4 Rewrite `src/Njord/Configuration/TelemetrySetupContainer.cs`: `AddOpenTelemetry().ConfigureResource().WithMetrics()` with `AddMeter` per Njord meter, `AddAspNetCoreInstrumentation()`, `AddProcessInstrumentation()`, `AddRuntimeInstrumentation()`, `AddPrometheusExporter()`
- [x] 3.5 Remove `NjordMeters` static set from `TelemetrySetupContainer`
- [x] 3.6 Update `src/Njord/Configuration/NjordApplicationSetup.cs`: replace `app.UseHttpMetrics()` + `app.MapMetrics()` with `app.MapPrometheusScrapingEndpoint()`
- [x] 3.7 Update any references to `TelemetrySetupContainer.NjordMeters` across the codebase

## 4. Config and Docs

- [x] 4.1 Update `docker-compose.example.yml` if it references `Njord:Persistence:Provider`
- [x] 4.2 Update `src/Njord/appsettings.Example.json` if it references Provider
- [x] 4.3 Update Config Builder (`docs/.vitepress/theme/builder/`) if it references Provider or prometheus-net
- [x] 4.4 Update AGENTS.md persistence options table: remove Provider row, note ConnectionString detection

## 5. Validation

- [x] 5.1 `dotnet build src/Njord.slnx`
- [x] 5.2 `dotnet format src/Njord.slnx --verify-no-changes`
- [x] 5.3 Run all test projects: 1459 tests, 0 errors, 0 failures
- [ ] 5.4 Verify `dotnet run --project src/Njord/Njord.csproj` starts without errors (manual smoke test)
