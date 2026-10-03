## Context

njord logging currently uses three APIs that all funnel into Serilog:

1. **Akka `ILoggingAdapter`** via `Context.GetLogger()` — used in 4 actors (snapshot actors, `BudgetTrackerActor`, `ForecastHistoryActor`)
2. **MS `ILogger<T>`** via DI — used in 8 actors and 4 non-actor classes
3. **Serilog `LogContext.PushProperty`** — used in all 12 `ILogger<T>` files for manual `Subsystem` enrichment

A custom `Subsystem` property is set manually in every class and requires `IDisposable` scope management. Two gRPC services leak the scope (never dispose). Two static stream-builder methods accept `ILogger` as a parameter to log from lambdas.

## Goals / Non-Goals

**Goals:**
- One logging API per context: Akka `ILoggingAdapter` in actors, `ILogger<T>` in non-actor classes
- Replace custom `Subsystem` property with built-in `SourceContext` (set automatically by both APIs)
- Remove all `Serilog.Context` imports from application code — Serilog is infrastructure only (`Program.cs`)
- Eliminate manual scope management (`_logScope`, `PushProperty`, `Dispose`)

**Non-Goals:**
- Changing log levels or adding/removing log statements beyond what's needed for the migration
- Adding sinks, correlation IDs, or distributed tracing
- Changing the Akka.Logger.Serilog bridge configuration

## Decisions

### Decision 1: Actors use `Context.GetLogger()`, non-actors use `ILogger<T>`

**Choice:** Akka's `ILoggingAdapter` for all actor classes; `ILogger<T>` via DI for gRPC services, `MqttNetPublisher`, and `HistoryEnrichment`.

**Why:** `Context.GetLogger()` automatically includes the actor path in log output, which is valuable for actor lifecycle debugging. It's the idiomatic Akka pattern. Non-actor classes don't have an actor context, so `ILogger<T>` is the natural fit there.

**Alternative considered:** `ILogger<T>` everywhere. Rejected because it requires DI wiring for every actor and loses the actor path context that Akka's logger provides automatically.

### Decision 2: Drop `Subsystem`, use `SourceContext`

**Choice:** Remove all manual `Subsystem` property enrichment. Use the `SourceContext` property that both `ILogger<T>` and `Akka.Logger.Serilog` set automatically (to the class type name).

**Why:** `SourceContext` is more granular (per-class vs per-subsystem), requires zero code, and is the standard property that Serilog's `MinimumLevel.Override` already understands. Log filtering by namespace (`Njord.Pipeline`, `Njord.Mqtt`) replaces subsystem filtering.

**Alternative considered:** Keep `Subsystem` alongside `SourceContext`. Rejected because it's redundant — the namespace already implies the subsystem, and maintaining both adds boilerplate for no benefit.

### Decision 3: Move logging out of static stream-builder methods

**Choice:** `ModelStateActor.BuildCapabilityLearned` and `EnrichmentActor.BuildConsensusInlineFlow` stop accepting an `ILogger` parameter and stop logging internally. The calling actor logs the result after the method returns.

**Why:** Static methods shouldn't own logging concerns. The actor has the logging context (actor path, lifecycle) and knows what happened. Passing loggers into static methods couples them to a specific logging API.

### Decision 4: Console output template with `SourceContext`

**Choice:** Change the Serilog console template from:
```
[{Timestamp:HH:mm:ss} {Level:u3}] [{Subsystem,-8}] {Message:lj}{NewLine}{Exception}
```
to:
```
[{Timestamp:HH:mm:ss} {Level:u3}] [{SourceContext}] {Message:lj}{NewLine}{Exception}
```

**Why:** Direct replacement. `SourceContext` is longer than the old 8-char subsystem tag but provides precise attribution. No padding — class names vary in length and fixed-width would waste space.

### Decision 5: Remove `ILogger<T>` DI registrations for migrated actors

**Choice:** Remove `ILogger<T>` constructor parameters from migrated actors. The actors no longer need DI-injected loggers since `Context.GetLogger()` is self-contained.

**Why:** Dead constructor parameters are confusing and add unnecessary DI overhead.

## Risks / Trade-offs

**[Longer log lines]** → `SourceContext` values like `Njord.Pipeline.SchedulerActor` are wider than `pipeline`. Acceptable — readability is better than alignment, and log aggregation tools don't care about column width.

**[Akka message template format]** → `ILoggingAdapter` uses Serilog message templates when `Akka.Logger.Serilog` is active (`{Location}` style, not `{0}` positional). This is already how the 4 existing Akka-logger actors work, so no format change needed. Risk: if the Serilog bridge were removed, templates would break. Mitigation: `Akka.Logger.Serilog` is a core dependency, not going away.

**[Test changes]** → Tests that assert on `Subsystem` property or inject `ILogger<T>` into actors need updating. Scope is bounded — actors under test typically use `TestKit` which provides its own logging.

## Open Questions

None — all decisions were made during explore.
