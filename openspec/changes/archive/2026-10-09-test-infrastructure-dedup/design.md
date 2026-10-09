## Context

FunkArr uses `FunkArrFixture` (centralized probe registry, IAsyncLifetime lifecycle) and
`TestItemTraceBuilder` (builder pattern for complex test data) to avoid per-test
boilerplate. njord has `Njord.Tests.Shared` (131 lines across 8 files) but doesn't provide
a base spec class or factory helpers, leading to repeated constants, property wrappers,
and configuration setup across 15+ test files.

## Goals / Non-Goals

**Goals:**
- One change to each test file reduces it by ~20 lines of setup boilerplate.
- Time constants, FakeTimeProvider creation, and ActorRegistry access shared once.
- Actor specs share a base class with test persistence + timefactor pre-configured.

**Non-Goals:**
- Not introducing a test-data builder pattern (domain types are simple enough).
- Not changing test assertion conventions.
- Not removing stateful fakes (they stay per convention).

## Decisions

1. **`TestTimeConstants`** (new, `Njord.Tests.Shared`): static class with 2-3 named
   `DateTimeOffset` constants currently hardcoded in 7+ files. Simple, mechanical.

2. **`FakeTimeProvider` factory**: `TestTimeouts.AtEpoch(DateTimeOffset? offset = null)`
   returning `new FakeTimeProvider(offset ?? TestTimeConstants.PipelineEpoch)`.

3. **`NjordActorSpec` base class** (new, `Njord.Tests.Shared`): extends
   `Akka.Hosting.TestKit.TestKit`, provides:
   - `protected IActorRef GetActor<TKey>()` → `ActorRegistry.Get<TKey>()`
   - Default `ConfigureServices` registering `TestOptionsMonitor<NjordOptions>`,
     `FakeTimeProvider`, shared fakes
   - Default `ConfigureAkka` with in-memory persistence + `akka.test.timefactor = 3`
   - Virtual override points for per-spec customization

   Alternative: keep everything as extension methods without a base class. Rejected — the
   20-line ConfigureAkka boilerplate is the biggest duplication source and can only be
   shared through inheritance (TestKit requires overriding `ConfigureAkka`).

## Risks / Trade-offs

- [Risk] A base class couples specs to a shared configuration shape. → Mitigated by
  virtual override points; specs that need different config can override.

## Migration Plan

Additive first (add base class + helpers), then migrate specs one project at a time.

## Open Questions

None.
