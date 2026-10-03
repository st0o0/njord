## Context

Three specs contain custom collector classes introduced during the
`deterministic-testkit-tests` change. At that time, TestKit base classes were
not yet available (specs used raw ActorSystem). Now that every spec inherits
from TestKit/PersistenceTestKit, `CreateTestProbe()` is universally available.

## Goals / Non-Goals

**Goals:**
- Remove all `MessageCollector<T>` and `OfferCollector` classes.
- All assertions use TestProbe's `ExpectMsg`, `ExpectNoMsg`, `FishForMessage`,
  or `ReceiveWhile` — no custom signaling infrastructure.
- Fix `.Result` blocking calls in fake actors (use `PipeTo`).

**Non-Goals:**
- Changing `RecordingTransport` in MqttConnectionActorSpec (not actor-based).
- Adding or removing test cases.

## Decisions

### Stream → TestProbe routing

Use `Sink.ForEach(m => probe.Tell(m))` to route stream elements to a
TestProbe. Do NOT use `Sink.ActorRef(probe, ...)` — this caused SinkRef
subscription issues in the previous change.

### Replacing count-based patterns

The `WaitFor(countBefore + 1)` pattern tracked counts to detect "something
new arrived". With TestProbe, this becomes unnecessary:

```
Before:  await collector.WaitFor(1);
         var countBefore = collector.Count;
         actor.Tell(triggerAction);
         await collector.WaitFor(countBefore + 1);

After:   probe.ReceiveWhile<T>(idle: TimeSpan.FromMilliseconds(300));  // drain batch
         actor.Tell(triggerAction);
         await probe.ExpectMsgAsync<T>();  // new message = assertion passed
```

`ReceiveWhile` collects messages until the idle timeout expires, effectively
draining a batch. After draining, `ExpectMsg` blocks until the next message.

### Replacing WaitForMatch

`WaitForMatch(predicate)` → `FishForMessageAsync(predicate)`. TestProbe's
`FishForMessage` consumes messages until one matches the predicate, which is
the exact semantic.

### Fake actors: Tell instead of Add

Fake actors currently call `collector.Add(msg)`. Replace with
`probe.Tell(msg)` — identical call-site pattern, same fire-and-forget
semantics.

### FakePipelineActor .Result fix

SchedulerActorSpec's `FakePipelineActor` uses `.Result` to block on
`StreamRefs.SinkRef<>().Run(mat)` and `StreamRefs.SourceRef<>().Run(mat)`.
Replace with `PipeTo` to avoid dispatcher deadlocks (same fix applied to
DiscoveryActorSpec's FakeEgressSourceProvider in the previous change).

## Risks / Trade-offs

**[ReceiveWhile idle timeout adds ~300ms to drain-tests]** → Only affects
2 tests (Ha_birth, Late_capability). Acceptable given the determinism gain.
Can be tuned down to 100ms if needed.

**[FishForMessage consumes non-matching messages]** → Fine for these tests
since we don't assert on consumed messages. If a test needed to inspect
skipped messages, ReceiveWhile with a filter would be the alternative.
