## Why

A cross-codebase review against the sibling FunkArr project found that njord's test
projects repeat identical boilerplate that FunkArr avoids through shared infrastructure
(`FunkArrFixture` with centralized probe registry, `TestItemTraceBuilder` for complex test
data). Concretely in njord:
1. The same `DateTimeOffset` constants (e.g. `new(2026, 7, 12, 6, 0, 0, TimeSpan.Zero)`)
   appear hardcoded in 7+ Pipeline test files.
2. `FakeTimeProvider` is instantiated inline 60+ times across all test projects without a
   factory helper.
3. `ActorRegistry.Get<T>()` is wrapped in a private property 9 times across Pipeline specs
   (`SchedulerActorSpec`, `SchedulerActorRefFailureSpec`, `SchedulerActorGetPollStatesSpec`,
   `SchedulerActorSnapshotSpec`, `SchedulerActorStartupOrderSpec`).
4. Each actor spec repeats ~20 lines of `ConfigureServices`/`ConfigureAkka` boilerplate
   (test persistence, timefactor, default NjordOptions, fake registration).

## What Changes

- Add `TestTimeConstants` to `src/Njord.Tests.Shared/` with the shared epoch constants.
- Add a `FakeTimeProvider` factory method to `TestTimeouts` (or a new helper):
  `public static FakeTimeProvider AtEpoch(DateTimeOffset? offset = null)`.
- Add an abstract `NjordActorSpec : Akka.Hosting.TestKit.TestKit` base class to
  `src/Njord.Tests.Shared/` providing: `GetActor<TKey>()` helper, default
  `ConfigureServices`/`ConfigureAkka` with test persistence + timefactor + default options,
  overridable hooks for per-spec customization.
- Migrate the Pipeline, Enrichment, Mqtt, and Egress actor specs to use the base class,
  removing per-spec boilerplate.

## Capabilities

### New Capabilities

- `test-shared-infrastructure`: shared base class, time constants, and factory helpers
  for actor specs — reducing per-spec boilerplate.

### Modified Capabilities

None.

## Impact

- `src/Njord.Tests.Shared/` (new files)
- `src/Njord.Pipeline.Tests/` (all actor specs)
- `src/Njord.Enrichment.Tests/` (actor specs)
- `src/Njord.Mqtt.Tests/` (actor specs)
- `src/Njord.Egress.Tests/` (actor specs)
- No production code changes; test behavior unchanged.

## Non-goals

- Not introducing a test-data builder pattern (like FunkArr's `TestItemTraceBuilder`) —
  njord's domain types are simpler records that don't need builders yet.
- Not replacing the 6 stateful/recording fakes (those stay per the NSubstitute convention).
- Not changing test assertion conventions.

## API budget impact

None.
