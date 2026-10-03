## Context

All actor/stream specs inherit from `PersistenceTestKit` which provides `Sys`
(the actor system) and TestKit infrastructure. Currently, specs create fake
actors that collect results in shared `List<T>` or `TaskCompletionSource`,
then poll with `AsyncAssert.WaitUntil`. This is non-deterministic on CI.

## Goals / Non-Goals

**Goals:**
- Every assertion that currently uses `AsyncAssert.WaitUntil` becomes
  deterministic using TestKit's `TestProbe` or `ExpectMsg`.
- Tests pass reliably on CI regardless of thread scheduling.

**Non-Goals:**
- Changing what the tests assert.
- Adding new test coverage.

## Decisions

### Pattern: TestProbe as message sink

Instead of collecting into a `List<T>` polled by the test thread, route
messages to a `TestProbe` and use `ExpectMsg<T>` which blocks
deterministically until the message arrives (with a configurable timeout
backed by the TestKit's dilated time).

```
Before:
  var received = new List<T>();
  actor → Sink.ForEach(t => received.Add(t))
  await AsyncAssert.WaitUntil(() => received.Count >= N);

After:
  var probe = CreateTestProbe();
  actor → Sink.ActorRef(probe)
  probe.ExpectMsg<T>();  // deterministic, no polling
```

### Pattern: ExpectNoMsg for "nothing happened" assertions

`AsyncAssert.StaysTrue(() => count == X)` polls for 300ms to verify nothing
changed. Replace with `probe.ExpectNoMsg(TimeSpan.FromMilliseconds(300))` —
deterministic, same semantics.

### Pattern: OfferCollector stays for SchedulerActorSpec

The `OfferCollector` in `SchedulerActorSpec` already uses `TaskCompletionSource`
(not polling). It's deterministic and can stay. Only the 3 `StaysTrue` calls
need `ExpectNoMsg`.

### Migration order

Migrate specs from simplest to most complex:
1. `ForecastHistoryActorSpec` (2 usages, simple Ask-based)
2. `SchedulerActorSpec` (3 StaysTrue → ExpectNoMsg)
3. `ModelStateActorSpec` (5 usages, stream-based)
4. `MqttConnectionActorSpec` (4 usages)
5. `DiscoveryActorSpec` (13 usages, most complex)
6. `SinkRefConnectionSpec` (1 usage)

## Risks / Trade-offs

**[TestProbe adds actor overhead per test]** → Negligible. TestProbe is a
lightweight actor designed for testing.

**[ExpectMsg has a timeout too]** → Yes, but it's backed by TestKit's dilated
time which adapts to CI slowness. And it doesn't poll — it blocks on the
actor mailbox, which is deterministic.
