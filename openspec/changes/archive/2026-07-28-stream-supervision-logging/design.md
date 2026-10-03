## Context

All 8 stream graphs use an identical pattern:

```csharp
.WithAttributes(ActorAttributes.CreateSupervisionStrategy(
    _ => Akka.Streams.Supervision.Directive.Resume))
```

The `_` discards the exception — no logging, no type inspection. This was a
pragmatic "keep the stream alive" choice during initial development but makes
debugging production issues nearly impossible.

## Goals / Non-Goals

**Goals:**

- Make every stream exception visible in logs (Warning level).
- Resume on transient/expected exceptions (AskTimeoutException,
  TaskCanceledException, OperationCanceledException).
- Escalate on unexpected exceptions (bugs) so the actor's supervision strategy
  can restart the stream cleanly.
- Align MQTT Enabled defaults between NjordServiceSetup and MqttOptions.

**Non-Goals:**

- Adding retry logic within streams.
- Changing actor supervision strategies.
- Making the decider configurable at runtime.

## Decisions

### D1: Shared static decider factory with ILogger

**Decision:** Create a static helper `StreamSupervision.LoggingDecider(ILogger)`
in `Njord.Pipeline` that returns a `Decider` function. The decider logs every
exception at Warning level, then returns `Resume` for transient types and
`Stop` for unexpected types.

**Transient (Resume):**
- `AskTimeoutException` — downstream actor didn't respond in time
- `TaskCanceledException` — cancellation during async operations
- `OperationCanceledException` — same family
- `TimeoutException` — HTTP or other timeout
- `HttpRequestException` — transient network errors

**Unexpected (Stop):**
- Everything else (NullReferenceException, InvalidOperationException,
  JsonException, etc.)

**Why Stop, not Restart:** `Stop` terminates the failed stream stage. The actor
holding the stream gets a `StreamCompletedWithError` or similar, which triggers
Akka's actor supervision (default: Restart). This rematerializes the stream
cleanly. `Resume` on a bug would just keep dropping elements.

**Why a static helper, not a base class:** The decider is used in different
contexts (actors, enrichment features). A static factory is the simplest
approach that works everywhere.

**Alternatives considered:**
- Per-graph custom deciders — too much duplication, same logic everywhere.
- Global actor system-level decider — Akka.Streams doesn't support that.
- Logging inside each `WithAttributes` call inline — duplicates the pattern.

### D2: MQTT default — change ServiceSetup to match MqttOptions

**Decision:** Change `GetValue("Enabled", true)` to `GetValue("Enabled", false)`
in NjordServiceSetup.cs. This aligns with MqttOptions.Enabled defaulting to
`false`. CLAUDE.md already says "MQTT is disabled by default."

**Alternatives considered:**
- Reading from bound options — would require options to be bound before service
  registration, which isn't guaranteed in the registration order.

## Risks / Trade-offs

- **[New log noise]** Previously-silent exceptions will now appear as Warning
  logs. → This is the entire point. Expected transient errors (timeouts) are
  logged at Warning which is appropriate — they indicate temporary conditions
  worth investigating if frequent.

- **[Stop vs Resume for unknown exceptions]** Stopping the stream stage means
  that stream segment stops processing. The actor restarts via supervision and
  rematerializes. → Acceptable — a bug-induced exception would have silently
  dropped data before; now it triggers a visible restart. Brief data gap is
  better than ongoing silent data loss.

- **[MQTT default change]** Existing deployments that relied on the implicit
  `true` default without setting `Mqtt:Enabled` explicitly will now need to add
  it. → Low risk: the CLAUDE.md explicitly says to enable via
  `Njord__Mqtt__Enabled=true`, and docker-compose/env configs already set it.
