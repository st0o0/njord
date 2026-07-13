## 1. Package Setup

- [x] 1.1 Add `Servus` (0.34.0) and `Servus.Akka` (0.3.13) to `src/Directory.Packages.props`
- [x] 1.2 Add package references to `src/Njord/Njord.csproj` via `dotnet add package Servus` and `dotnet add package Servus.Akka`
- [x] 1.3 Verify solution builds: `dotnet build Njord.slnx`

## 2. NjordServiceSetup

- [x] 2.1 Create `src/Njord/Configuration/NjordServiceSetup.cs` implementing `IServiceSetupContainer` — move options binding (`NjordOptions`, `EnrichmentOptions`, validator), `ParameterRegistry` resolution, `TimeProvider`, `AddOpenMeteoIngest()`, and `AddMqttEgress()` from `Program.cs` into `SetupServices`
- [x] 2.2 Write `src/Njord.Tests/Configuration/NjordServiceSetupSpec.cs` — verify that `SetupServices` registers `IOpenMeteoClient`, `IMqttConnection`, `IMqttTransport`, `IOptions<NjordOptions>`, `TimeProvider`, and `ResolvedParameterSet` in the service collection

## 3. NjordActorSystemSetup

- [x] 3.1 Create `src/Njord/Configuration/NjordActorSystemSetup.cs` extending `ActorSystemSetupContainer` — override `GetActorSystemName()` returning `"njord"`, move persistence HOCON and actor registration into `BuildSystem`. Use `WithResolvableActors` to register `MqttEgressActor` (`"mqtt-egress"`), `PipelineActor` (`"pipeline"`), `SchedulerActor` (`"scheduler"`), `EnrichmentActor` (`"enrichment"`)
- [x] 3.2 Write `src/Njord.Tests/Configuration/NjordActorSystemSetupSpec.cs` — verify actor system name is `"njord"` and all four actors are resolvable from `IActorRegistry`

## 4. Refactor Program.cs

- [x] 4.1 Replace inline DI and Akka wiring in `src/Njord/Program.cs` with calls to `NjordServiceSetup.SetupServices(services, config)` and `NjordActorSystemSetup.SetupServices(services, config)`. Keep only `builder.Services.AddHealthChecks()` and `app.MapHealthChecks("/healthz")` inline
- [x] 4.2 Verify all existing tests still pass: `dotnet run --project Njord.Tests/Njord.Tests.csproj`

## 5. Actor Registry Extensions

- [x] 5.1 Refactor `src/Njord/Pipeline/PipelineActor.cs` — remove `ActorRegistry` constructor parameter and `_registry` field; replace `_registry.Get<SchedulerActor>()` with `Context.GetActor<SchedulerActor>()`
- [x] 5.2 Refactor `src/Njord/Egress/MqttEgressActor.cs` — remove `ActorRegistry` constructor parameter and `_registry` field; replace `_registry.Get<PipelineActor>()` calls (in `PreStart` and `RequestNewSourceRef`) with `Context.GetActor<PipelineActor>()`
- [x] 5.3 Refactor `src/Njord/Enrichment/EnrichmentActor.cs` — remove `ActorRegistry` constructor parameter and `_registry` field; replace `_registry.Get<PipelineActor>()` and `_registry.Get<MqttEgressActor>()` calls (in `PreStart` and `HandleTerminated`) with `Context.GetActor<T>()`
- [x] 5.4 Refactor `src/Njord/Pipeline/SchedulerActor.cs` — remove `ActorRegistry` constructor parameter and `_registry` field (not used in message handlers, only injected)
- [x] 5.5 Update all actor tests that construct actors with `ActorRegistry` to remove that parameter — affected: `src/Njord.Tests/Pipeline/PollPipelineSpec.cs`, `src/Njord.Tests/Pipeline/SchedulerActorSpec.cs`, `src/Njord.Tests/Egress/MqttEgressIntegrationSpec.cs`, `src/Njord.Tests/Enrichment/EnrichmentActorSpec.cs`

## 6. ResolveChildActor for ForecastHistoryActor

- [x] 6.1 No constructor reorder needed — `ActivatorUtilities` matches by type regardless of order
- [x] 6.2 Refactor `src/Njord/Enrichment/EnrichmentActor.cs` `MaterializeHistoryConsumer` — replace `Props.Create(() => new ForecastHistoryActor(...))` with `Context.ResolveChildActor<ForecastHistoryActor>(name, location, historyOptions)`
- [x] 6.3 No test changes needed — tests create actor directly via `Props.Create`, constructor unchanged

## 7. Validation

- [x] 7.1 Full test suite passes: `dotnet run --project Njord.Tests/Njord.Tests.csproj`
- [x] 7.2 Solution builds clean with no warnings: `dotnet build Njord.slnx --warnaserror`
- [x] 7.3 Run `dotnet slopwatch` from repo root to verify no reward-hacking regressions
