## Why

Every log line of njord's own actors reaches Serilog with the category `Akka.Actor.ActorSystem`, because
Akka.Hosting's `LoggerFactoryLogger` (behind `loggers.AddLoggerFactory()`) writes all Akka log events through a
single `ILogger<ActorSystem>`. The production override `"Akka": "Warning"` therefore hides every Information
log of njord's actors (lifecycle messages such as "Gateway resolved - scheduling initial polls"), per-namespace
overrides such as `"Njord.Pipeline": "Debug"` never match an actor log, and actor Debug logs never appear at all
because Akka's own `akka.loglevel` stays at its default `INFO`. The main specs already promise a SourceContext of
`Njord.Pipeline.PollScheduler`; this was never verified and is not true today. The upcoming stream changes
(stable pipeline input/consumers, poll workers) depend on readable actor logs, so this goes first.

## What Changes

- A njord logger actor replaces Akka.Hosting's `LoggerFactoryLogger` in the actor system's `akka.loggers`. It
  writes each Akka log event to an `ILogger` whose category is the full type name of the event's logging class
  (`LogEvent.LogClass`): an actor's `Context.GetLogger()` events carry the actor class
  (`Njord.Pipeline.PollScheduler`), Akka's internal events keep their `Akka.*` type, so the `"Akka"` override
  filters only Akka internals. The structured properties of the Akka.Hosting bridge (template, semantic
  properties, `ActorPath`, `LogSource`, `Thread`, `Timestamp`, trace ids) stay the same.
- `akka.loglevel` is derived from the Serilog configuration: the lowest of `Serilog:MinimumLevel:Default` and
  every `Serilog:MinimumLevel:Override` value, mapped to Akka's four levels. Production (`Information` default)
  stays at `INFO`; a `Debug` default or override (for example `"Njord.Pipeline": "Debug"` or the Development
  settings) lowers it to `DEBUG`, so actor Debug logs reach Serilog, which then filters per category.
- Actors keep `private readonly ILoggingAdapter _log = Context.GetLogger();` unchanged.
- `appsettings.Example.json` documents per-actor overrides by category (`Njord.Mqtt.MqttStatePublisher` for the
  MQTT `.Log()` stages) instead of the `.Log()` stage names, which are not log categories. The configuration
  docs gain a short logging section.

## Non-goals

- No change to log messages, levels or templates of any actor or service.
- No Serilog dependency outside `Program.cs` and the tests; Serilog level switches (`LevelSwitches`,
  `ControlledBy`) and runtime log level reload are not supported for the Akka level.
- No OpenTelemetry log export or trace correlation work beyond keeping the existing trace-id properties.
- Non-actor classes keep `ILogger<T>` from DI; nothing changes for them.

## API budget

None. The change touches logging only; polling and Open-Meteo requests are unaffected.

## Capabilities

### New Capabilities

None.

### Modified Capabilities

- `structured-logging`: actor log SourceContext is the actor's full type name (now guaranteed by njord's logger
  actor, not assumed from the Akka.Hosting bridge); new requirements for Akka internal categories and for the
  derived Akka log level; the example-config requirement documents per-actor category overrides.
- `stream-log-tracing`: stream tracing is enabled by lowering the level of the owning actor's category (its
  full type name).

## Impact

- `src/Njord/Configuration/AkkaSetupContainer.cs` (logger registration, `akka.loglevel`), new logger actor and
  level resolution in `src/Njord/Configuration/`.
- `src/Njord.Tests` (new specs; production `appsettings.json` linked into the test output).
- `src/Njord/appsettings.Example.json` comments, `docs/configuration/`, `AGENTS.md` test totals.
- Operators: njord actor Information logs become visible in production output (a handful of lifecycle lines at
  startup and on config or connection changes, per the existing level discipline).
