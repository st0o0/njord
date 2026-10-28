## 1. SupervisorDefaults

- [x] 1.1 Create `src/Njord.Core/Actors/SupervisorDefaults.cs` with `MinBackoff` (3 s), `MaxBackoff` (30 s), `RandomFactor` (0.2) as public static readonly fields
- [x] 1.2 Update `src/Njord.Budget/Configuration/BudgetSetupContainer.cs` -- replace local `MinBackoff`/`MaxBackoff`/`RandomFactor` with `SupervisorDefaults.*`
- [x] 1.3 Update `src/Njord.Grpc/Configuration/GrpcSetupContainer.cs` -- replace local constants with `SupervisorDefaults.*`
- [x] 1.4 Update `src/Njord.Pipeline/Configuration/PipelineSetupContainer.cs` -- replace local constants with `SupervisorDefaults.*`
- [x] 1.5 Update `src/Njord.OpenMeteo/Configuration/OpenMeteoSetupContainer.cs` -- replace local constants with `SupervisorDefaults.*`
- [x] 1.6 Update `src/Njord.Polling/Configuration/PollingRegistrationExtensions.cs` -- replace local constants with `SupervisorDefaults.*`

## 2. LoggingSetupContainer

- [x] 2.1 Create `src/Njord/Configuration/LoggingSetupContainer.cs` -- extract Serilog config (`AddSerilog`, `ReadFrom.Configuration`, enrichers, console sink template) and `builder.Logging.ClearProviders()` from `NjordHost.Create`
- [x] 2.2 Remove inline Serilog and logging config from `src/Njord/NjordHost.cs`
- [x] 2.3 Add `WithSetup<LoggingSetupContainer>()` as the first entry in the `NjordHost.Create` chain

## 3. TelemetrySetupContainer

- [x] 3.1 Create `src/Njord/Configuration/TelemetrySetupContainer.cs` -- move `Metrics.ConfigureMeterAdapter` and `NjordMeters` set from `CoreSetupContainer`
- [x] 3.2 Remove meter adapter config and `NjordMeters` from `src/Njord/Configuration/CoreSetupContainer.cs`
- [x] 3.3 Add `WithSetup<TelemetrySetupContainer>()` second in the chain (after LoggingSetupContainer)

## 4. CoreSetupContainer cleanup

- [x] 4.1 Move Kestrel `MinResponseDataRate = null` config from `NjordHost.Create` into `src/Njord/Configuration/CoreSetupContainer.cs`
- [x] 4.2 Remove inline `ConfigureKestrel` from `src/Njord/NjordHost.cs`
- [x] 4.3 Verify CoreSetupContainer now contains only `TimeProvider.System` registration and Kestrel config

## 5. AkkaSetupContainer

- [x] 5.1 Update `src/Njord/Configuration/AkkaSetupContainer.cs` `ConfigureInfrastructure` -- add `journalBuilder: j => j.WithHealthCheck()` and `snapshotBuilder: s => s.WithHealthCheck()` to `WithSqlPersistence`
- [x] 5.2 Add `.WithActorSystemLivenessCheck()` and `.WithAkkaClusterReadinessCheck()` after clustering config
- [x] 5.3 Replace `loggers.AddLogger<ActorCategoryLogger>()` with `loggers.AddLoggerFactory()` in `ConfigureLogging`
- [x] 5.4 Delete `src/Njord/Configuration/ActorCategoryLogger.cs`
- [x] 5.5 Remove the `ActorCategoryLogger` using/reference from `AkkaSetupContainer.cs`

## 6. NjordHost slimming

- [x] 6.1 Verify `src/Njord/NjordHost.cs` contains only the `AppBuilder.Create` + `WithSetup<>` chain with the correct order: Logging, Telemetry, Core, Config, Health, OpenMeteo, Budget, Polling, Pipeline, Domain, Grpc, Akka, NjordApplicationSetup
- [x] 6.2 Remove unused `using` directives from `NjordHost.cs`

## 7. Validation

- [x] 7.1 `dotnet build src/Njord.slnx` -- verify clean build
- [x] 7.2 `dotnet format src/Njord.slnx --verify-no-changes` -- verify formatting
- [x] 7.3 Run all test projects:
  - `dotnet run --project src/Njord.Core.Tests/Njord.Core.Tests.csproj`
  - `dotnet run --project src/Njord.Messages.Tests/Njord.Messages.Tests.csproj`
  - `dotnet run --project src/Njord.Persistence.Tests/Njord.Persistence.Tests.csproj`
  - `dotnet run --project src/Njord.OpenMeteo.Tests/Njord.OpenMeteo.Tests.csproj`
  - `dotnet run --project src/Njord.Budget.Tests/Njord.Budget.Tests.csproj`
  - `dotnet run --project src/Njord.Polling.Tests/Njord.Polling.Tests.csproj`
  - `dotnet run --project src/Njord.Pipeline.Tests/Njord.Pipeline.Tests.csproj`
  - `dotnet run --project src/Njord.Features.Tests/Njord.Features.Tests.csproj`
  - `dotnet run --project src/Njord.Grpc.Tests/Njord.Grpc.Tests.csproj`
  - `dotnet run --project src/Njord.IntegrationTests/Njord.IntegrationTests.csproj`
  - `dotnet run --project src/Njord.Architecture.Tests/Njord.Architecture.Tests.csproj`
