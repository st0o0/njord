## Why

njord's setup containers have accumulated inconsistencies compared to the FunkArr reference: Serilog config is inline in NjordHost, telemetry and TimeProvider are mixed in CoreSetupContainer, supervisor parameters are duplicated across five IActorRegistration implementations, AkkaSetupContainer lacks Akka's built-in health checks (journal, liveness, cluster readiness), and ActorCategoryLogger adds ~100 lines of custom code that deviates from the telemetry-infrastructure spec (which already prescribes `AddLoggerFactory()`). One concern per container, no inline config in the host.

## What Changes

- Extract Serilog configuration from `NjordHost.Create` into a new `LoggingSetupContainer`
- Create `TelemetrySetupContainer` for the Prometheus meter adapter (currently in CoreSetupContainer)
- Clean up `CoreSetupContainer` to register only `TimeProvider.System` and Kestrel configuration
- Centralize BackoffOnFailure supervisor defaults (3 s / 30 s / 0.2) into `SupervisorDefaults` in `Njord.Core/Actors/`; update all five `IActorRegistration` implementations to use the shared constants
- Add `WithHealthCheck()` on journal and snapshot persistence builders, `WithActorSystemLivenessCheck()`, and `WithAkkaClusterReadinessCheck()` to `AkkaSetupContainer`
- Replace `ActorCategoryLogger` with standard `AddLoggerFactory()` (Akka.Hosting) -- brings the code in line with the telemetry-infrastructure spec
- Delete `ActorCategoryLogger.cs`
- Slim `NjordHost.Create` to a pure `AppBuilder.Create` + `WithSetup<>` chain with no inline configuration

## Capabilities

### New Capabilities

None -- this is a structural cleanup of existing setup containers.

### Modified Capabilities

- `domain-setup-containers`: Container responsibilities change -- new LoggingSetupContainer and TelemetrySetupContainer, CoreSetupContainer scope narrows, supervisor defaults centralized, NjordHost loses inline config
- `structured-logging`: Actor log category changes from per-type (ActorCategoryLogger) to `ActorSystem` (standard AddLoggerFactory); per-type filtering moves from SourceContext to the LogSource property
- `health-checks`: Add Akka persistence journal/snapshot health checks, actor system liveness check, and cluster readiness check alongside the existing custom JournalHealthCheck

## Non-goals

- No polling changes -- zero API-budget impact
- Not changing the `IActorRegistration` pattern (libraries keep owning their actor registration)
- Not moving `IActorRegistration` to Servus (future work)
- Not migrating from prometheus-net to the OpenTelemetry SDK
- Not changing domain-specific setup containers beyond the supervisor defaults update

## Impact

- **Host project**: 2 new files (LoggingSetupContainer, TelemetrySetupContainer), 1 new file in Core (SupervisorDefaults), 3 edited (CoreSetupContainer, AkkaSetupContainer, NjordHost), 1 deleted (ActorCategoryLogger)
- **5 library projects** (Budget, Grpc, Pipeline, OpenMeteo, Polling): updated to use `SupervisorDefaults` instead of local constants
- **Integration tests**: `NjordHost.Create` signature is unchanged; tests should keep working
- **Logging output**: Actor logs will show as `Akka.Actor.ActorSystem` SourceContext instead of per-type; `LogSource` property still carries the actor path for filtering
- **Health endpoints**: `/healthz` gains the Akka persistence, liveness and readiness checks; the custom `JournalHealthCheck` (SQLite directory / PostgreSQL connectivity) is kept alongside for defense in depth
