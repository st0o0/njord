## Context

See proposal.md - Why. The current wiring in `AkkaSetupContainer.ConfigureInfrastructure` is
`ConfigureLoggers(l => { l.ClearLoggers(); l.AddLoggerFactory(); })`. Inspecting Akka.Hosting 1.5.73
(`Akka.Hosting.Logging.LoggerFactoryLogger`, decompiled) shows the cause:

```csharp
_akkaLogger = _loggerFactory.CreateLogger<ActorSystem>();   // constructor, once
...
protected virtual void Log(LogEvent log, ActorPath path)
{
    // builds an internal AkkaLogState (semantic properties, ActorPath, Timestamp, Thread, LogSource,
    // {OriginalFormat}, Akka.TraceId/SpanId/TraceFlags) and always calls _akkaLogger.Log(...)
}
```

Every event goes to the single category `Akka.Actor.ActorSystem`; `LogEvent.LogSource` (the actor path) and
`LogEvent.LogClass` are only properties. `LoggerConfigBuilder.LogLevel` is unset, so `akka.loglevel` keeps
Akka's default `INFO` and `BusLogging` never creates Debug events.

In Akka 1.5.73, `Context.GetLogger()` builds `BusLogging` with `LogSource.Create(context, system)`, whose type
is `context.Props.Type` - the actor class. String sources get `Akka.Event.DummyClassForStringSources`, Akka
components their own `Akka.*` type. So `LogClass` is exactly the category we want, for actors and internals.

## Goals / Non-Goals

**Goals:**
- Category = full type name of `LogEvent.LogClass`, with the same structured properties as the Akka.Hosting
  bridge.
- `akka.loglevel` low enough that Serilog sees every Debug event its configuration could show, and no lower.

**Non-Goals:**
- Changing actor code or the `_log` convention; changing Serilog setup in `Program.cs`.
- Live reload of the Akka level when `appsettings*.json` changes (Serilog's `ReadFrom.Configuration` does not
  reload levels either).

## Decisions

### D1: A njord logger actor instead of `AddLoggerFactory()`

`ActorCategoryLogger` (host, `Njord.Configuration`) is a sealed `ActorBase` implementing
`IRequiresMessageQueue<ILoggerMessageQueueSemantics>`, registered with `loggers.AddLogger<ActorCategoryLogger>()`.
It takes the `ILoggerFactory` from the `LoggerFactorySetup` that Akka.Hosting adds to every actor system
built from DI, answers `InitializeLogger` with `LoggerInitialized`, and for each `LogEvent` logs to
`loggerFactory.CreateLogger(category)` (cached per `LogClass`). The state is a key/value list with the same keys
as Akka.Hosting's `AkkaLogState` (semantic properties via `LogEventExtensions.TryGetProperties`, `ActorPath`,
`Timestamp`, `Thread`, `LogSource`, `{OriginalFormat}` via `GetTemplate`, `Akka.TraceId/SpanId/TraceFlags`
when present), so Serilog renders the same message and properties as before. Category: `LogClass.FullName`;
for a generic type the generic definition's name; `Akka.Actor.ActorSystem` if no class is known.

Alternatives:
- *Subclass `LoggerFactoryLogger` and override `Log`*: the factory field is private and `AkkaLogState` is
  internal, so the override would rebuild everything anyway while depending on a public-but-unsealed class's
  constructor side effects (`CreateLogger<ActorSystem>`). No gain.
- *Serilog enricher rewriting `SourceContext` from the `LogSource` property*: Serilog's
  `MinimumLevel.Override` is resolved from the category when the MEL logger is created, before enrichment,
  so `"Akka": "Warning"` would still drop the events; it also puts Serilog code outside `Program.cs` and maps
  actor paths, not classes.
- *`ILogger<T>` injected into actors*: violates the actor logging convention and loses `.Log()` stage output.

### D2: Derive `akka.loglevel` from the Serilog configuration

`AkkaLogLevel.From(IConfiguration)` returns the lowest level among `Serilog:MinimumLevel` (string form),
`Serilog:MinimumLevel:Default` and all `Serilog:MinimumLevel:Override:*` values; `Verbose`/`Debug` map to
`DebugLevel`, `Information` to `InfoLevel`, `Warning` to `WarningLevel`, `Error`/`Fatal` to `ErrorLevel`;
unparseable values are ignored and no value at all yields `InfoLevel` (Serilog's default).
`AkkaSetupContainer.ConfigureLogging(builder, configuration)` sets `loggers.LogLevel` from it.

Alternatives:
- *Always `DEBUG`*: simplest, but every Debug event of Akka internals (remoting, cluster, persistence,
  streams) and of every actor is formatted, published on the event stream and handed to the logger actor in
  production only to be dropped by Serilog. The derived level costs nothing and only differs when someone asks
  for Debug somewhere.
- *Asking `ILoggerFactory.CreateLogger(c).IsEnabled(Debug)`*: categories cannot be enumerated, so the minimum
  over all overrides is unknowable that way.
Reading the `Serilog` configuration section is plain `IConfiguration` access, no Serilog type is referenced
outside `Program.cs`.

### D3: Logging setup becomes its own method

`ConfigureInfrastructure` keeps persistence, remoting and clustering; logging moves to
`ConfigureLogging(AkkaConfigurationBuilder, IConfiguration)`, called from `BuildSystem`. Host specs use the same
method on a plain generic host (`AddAkka`), not the Akka.Hosting TestKit, because the TestKit installs its own
loggers.

## Risks / Trade-offs

- [Akka.Hosting changes the bridge's property set] → the njord logger owns its properties now; an upgrade cannot
  silently change them. Trace-context export via `AkkaTraceContextProcessor` keeps working because the same
  `Akka.TraceId/SpanId/TraceFlags` keys are emitted.
- [More visible output in production] → only the existing Information lifecycle logs of njord actors, which the
  level discipline (structured-logging-hygiene) keeps to startup, shutdown, config and connection changes.
- [Serilog level switches or levels set outside `Serilog:MinimumLevel`] → not covered by D2; Akka then stays at
  the level derived from what is configured. Documented in the configuration docs.
- [Actors created through a DI resolver report a different `Props.Type`] → verified by the manual run, where
  every njord actor line must show its class.

## Migration Plan

None beyond deploying. Operators who added `"Akka": "Information"` to see njord logs can remove it. Rollback is
reverting the commit.
