## Context

njord's setup containers grew organically and now diverge from the FunkArr reference pattern: one concern per container, no inline config in the host. The main issues are mixed responsibilities (CoreSetupContainer handles meters + TimeProvider), inline Serilog/Kestrel config in NjordHost, duplicated supervisor parameters across five IActorRegistration implementations, missing Akka-level health checks, and a custom ActorCategoryLogger that the telemetry-infrastructure spec already prescribes replacing with `AddLoggerFactory()`.

## Goals / Non-Goals

**Goals:**
- One concern per setup container (FunkArr pattern)
- NjordHost.Create is a pure composition chain
- Supervisor defaults defined once, used everywhere
- AkkaSetupContainer on par with FunkArr (persistence health checks, liveness, readiness)
- Code in line with existing specs (telemetry-infrastructure prescribes AddLoggerFactory)

**Non-Goals:**
- Migrating from prometheus-net to OpenTelemetry SDK
- Moving IActorRegistration to Servus
- Changing domain-specific container registrations beyond supervisor defaults
- Changing the WithSetup chain order (except inserting LoggingSetupContainer and TelemetrySetupContainer)

## Decisions

### 1. LoggingSetupContainer takes everything from NjordHost

Move all Serilog configuration and `builder.Logging.ClearProviders()` into `LoggingSetupContainer : IServiceSetupContainer`. This matches FunkArr's `LoggingSetupContainer` exactly. It becomes the first container in the chain (logging must be available before anything else).

**Alternative**: Keep logging in NjordHost as a "special" concern. Rejected because FunkArr proves it works as a container and the goal is consistency.

### 2. TelemetrySetupContainer owns meter configuration

Move `Metrics.ConfigureMeterAdapter` and the `NjordMeters` set from CoreSetupContainer to TelemetrySetupContainer. The static `NjordMeters` field becomes `TelemetrySetupContainer.NjordMeters` (public, for tests).

**Alternative**: Put meters in CoreSetupContainer alongside TimeProvider. Rejected because FunkArr separates these concerns.

### 3. CoreSetupContainer: TimeProvider + Kestrel

CoreSetupContainer keeps `TimeProvider.System` and gains the `ConfigureKestrel(MinResponseDataRate = null)` call currently inline in NjordHost. These are both "shared infrastructure for the host" concerns.

**Alternative**: Separate KestrelSetupContainer. Rejected -- too granular for a single line of config.

### 4. SupervisorDefaults in Njord.Core/Actors

```csharp
public static class SupervisorDefaults
{
    public static readonly TimeSpan MinBackoff = TimeSpan.FromSeconds(3);
    public static readonly TimeSpan MaxBackoff = TimeSpan.FromSeconds(30);
    public const double RandomFactor = 0.2;
}
```

All five IActorRegistration implementations reference these instead of local copies. Lives in `Njord.Core` because all libraries already reference Core.

**Alternative**: Put defaults in each library's own setup container. Rejected -- that is the current state and the duplication is the problem.

### 5. AkkaSetupContainer gains Akka health checks

Add to `ConfigureInfrastructure`:
- `WithSqlPersistence(..., journalBuilder: j => j.WithHealthCheck(), snapshotBuilder: s => s.WithHealthCheck())`
- `.WithActorSystemLivenessCheck()`
- `.WithAkkaClusterReadinessCheck()`

The existing custom `JournalHealthCheck` (SQLite dir writability / PostgreSQL connection) stays -- it checks a different layer (storage connectivity vs Akka persistence operations). Both fire on `/healthz`.

**Alternative**: Remove the custom JournalHealthCheck. Rejected -- defense in depth; they check different failure modes.

### 6. Replace ActorCategoryLogger with AddLoggerFactory

Change `ConfigureLogging` from:
```csharp
loggers.AddLogger<ActorCategoryLogger>();
```
to:
```csharp
loggers.AddLoggerFactory();
```

Delete `ActorCategoryLogger.cs`. The Akka log level derivation (`AkkaLogLevel.From(configuration)`) stays -- it reads Serilog config to set the Akka-level filter.

**Trade-off**: Actor logs lose per-type SourceContext (all show as `Akka.Actor.ActorSystem`). The `LogSource` property still carries the actor path. The `"Akka": "Warning"` Serilog override still works because the category is in the `Akka.Actor` namespace.

### 7. WithSetup chain order

```csharp
AppBuilder.Create(builder, build ?? (b => b.Build()))
    .WithSetup<LoggingSetupContainer>()      // NEW -- logging first
    .WithSetup<TelemetrySetupContainer>()     // NEW -- meters second
    .WithSetup<CoreSetupContainer>()          // CHANGED -- TimeProvider + Kestrel
    .WithSetup<ConfigSetupContainer>()
    .WithSetup<HealthSetupContainer>()
    .WithSetup<OpenMeteoSetupContainer>()
    .WithSetup<BudgetSetupContainer>()
    .WithSetup<PollingSetupContainer>()
    .WithSetup<PipelineSetupContainer>()
    .WithSetup<DomainSetupContainer>()
    .WithSetup<GrpcSetupContainer>()
    .WithSetup<AkkaSetupContainer>()
    .WithSetup<NjordApplicationSetup>()
    .Build();
```

LoggingSetupContainer goes first (like FunkArr), TelemetrySetupContainer second. The rest keeps the existing order.

## Risks / Trade-offs

- **[Actor log filtering]** Per-type SourceContext is lost. Operators who had Serilog overrides for specific actor types need to switch to LogSource-based filtering. Low risk -- njord is pre-1.0 and unlikely to have such overrides in production.
- **[Health check noise]** Three new Akka health checks (journal, snapshot, liveness, readiness) join `/healthz`. If any misbehave during startup, they could cause premature unhealthy reports. Mitigation: Akka.Hosting's health checks are mature and used in FunkArr without issues.
- **[AkkaLogLevel.From]** The log level derivation helper references the configuration. Keeping it in `AkkaSetupContainer.ConfigureLogging` ensures it still works. No risk.
- **[Integration tests]** `NjordHost.Create` signature is unchanged. The integration test fixture passes `build` delegate and should work as before. Run all tests to verify.

## Open Questions

None -- all decisions are straightforward FunkArr alignment.
