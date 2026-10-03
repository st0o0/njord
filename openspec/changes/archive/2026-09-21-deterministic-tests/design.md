## Context

CI tests fail intermittently on GitHub Actions runners (82 failed runs across various renovate/release branches) while passing locally. Root cause: the test suite has ~25 files with wall-clock timing dependencies. CI runners are slower and have variable load, so timing margins that work locally (200ms–1600ms) fail under contention.

Current state:
- **No TestKit timefactor** — all 24 Akka test classes extend `Akka.Hosting.TestKit.TestKit` directly with no shared HOCON config. Default timeout is 3s, timefactor is 1.0.
- **`TimeProvider.System` in ~15 test sites** — tests construct production types (`WeightedBudgetGate`, `ConsensusSnapshotFactory`, enrichments, actors) with the real clock instead of `FakeTimeProvider`.
- **`Thread.Sleep(1600)` in `BudgetThrottleStageSpec`** — waits for real token-bucket refill.
- **`Task.Delay` / `Task.WhenAny` races** — `StreamConsumerActorSpec` has 4 timing races with margins as tight as 200ms.
- **`DateTimeOffset.UtcNow` in test data** — ~10 sites seed test data from wall clock.
- **Two production code sites** bypass `TimeProvider`: `SensorGrpcService.cs:77` uses `DateTimeOffset.UtcNow`, `HistoryAnalyzer.cs:14` falls back to `TimeProvider.System` on null.

Existing `FakeTimeProvider` usage: most tests use the correct `Microsoft.Extensions.Time.Testing.FakeTimeProvider` with a fixed seed (`new DateTimeOffset(2026, 7, 12, 6, 0, 0, TimeSpan.Zero)`). Three enrichment specs define their own minimal `FakeTimeProvider` shim — these should migrate to the standard one.

## Goals / Non-Goals

**Goals:**
- Zero flaky CI runs from timing-dependent tests
- All tests use `FakeTimeProvider` or fixed timestamps — no wall-clock reads
- Akka TestKit timeouts scale automatically for slow environments via `timefactor`
- Production code has consistent `TimeProvider` seams — no direct `DateTime.UtcNow` / `DateTimeOffset.UtcNow`

**Non-Goals:**
- Changing production behavior or features
- Adding new tests or restructuring test project
- Addressing CI infrastructure issues (e.g., `hadolint/hadolint-action@v3`)
- Introducing a shared TestKit base class (each test class keeps its own `ConfigureAkka`)

## Decisions

### D1: Set `akka.test.timefactor = 3` via `AddHocon` in each test's `ConfigureAkka`

**Choice**: Add `builder.AddHocon("akka.test.timefactor = 3", HoconAddMode.Prepend)` in each TestKit-derived test class.

**Alternative considered**: Shared base class — rejected because each test class has different DI/Akka setup, and adding a base class is a larger refactor than needed. A static helper method in `Njord.Tests.Shared` that applies the config keeps it DRY without a base class.

**Alternative considered**: Environment-variable-based timefactor (higher in CI) — rejected as over-engineering. A fixed factor of 3 is sufficient: it makes the default 3s timeout into 9s, which is generous for CI without making tests slow locally.

### D2: `WeightedBudgetGate` — `FakeTimeProvider` + `Advance()` instead of `Thread.Sleep`

**Choice**: All `BudgetThrottleStageSpec` tests create `WeightedBudgetGate` with a `FakeTimeProvider`. The refill test calls `_time.Advance(TimeSpan.FromMilliseconds(1600))` instead of `Thread.Sleep(1600)`.

This is straightforward because `WeightedBudgetGate` already accepts `TimeProvider` as a constructor parameter.

### D3: `StreamConsumerActorSpec` — replace `Task.Delay` races with `AwaitConditionAsync`

**Choice**: Replace `Task.Delay(500)` + dead-letter counting with `AwaitConditionAsync(() => deadLetterCount >= expected)`. Replace `Task.WhenAny(tcs, Task.Delay(800))` with `AwaitConditionAsync(() => tcs.Task.IsCompleted)` with a generous timeout (e.g., 5s). Replace `Task.Delay(200)` "beat" with `AwaitConditionAsync` on the expected side-effect.

**Alternative considered**: `TestScheduler` (Akka's virtual-time scheduler) — rejected because `StreamConsumerActor` uses the system scheduler internally and switching to `TestScheduler` requires deeper changes to how the actor system is configured. `AwaitConditionAsync` with generous timeouts is simpler and sufficient.

### D4: Replace custom `FakeTimeProvider` shims with the standard one

**Choice**: The three enrichment specs (`AlertEnrichmentSpec`, `DerivedEnrichmentSpec`, `IndexEnrichmentSpec`) define their own `FakeTimeProvider` class. Replace with `Microsoft.Extensions.Time.Testing.FakeTimeProvider` for consistency. The custom shims only override `GetUtcNow()` — the standard `FakeTimeProvider` does the same plus supports `Advance()`.

### D5: Fixed timestamp epoch for all test data

**Choice**: Use the same fixed epoch already established in most tests: `new DateTimeOffset(2026, 7, 12, 6, 0, 0, TimeSpan.Zero)`. Replace all `DateTimeOffset.UtcNow` in test data construction (CycleId, ForecastPoint, NjordHealthState) with this fixed value or a `FakeTimeProvider.GetUtcNow()` call.

### D6: `SensorGrpcService` — inject `TimeProvider`

**Choice**: Add `TimeProvider` as a constructor parameter (DI-resolved). Replace `DateTimeOffset.UtcNow` fallback at line 77 with `_timeProvider.GetUtcNow()`. Already registered as singleton in DI (`NjordServiceSetup.cs:45`).

### D7: `HistoryAnalyzer` — make `TimeProvider` non-nullable

**Choice**: Change the parameter from `TimeProvider? timeProvider = null` to `TimeProvider timeProvider`. This is a static method — callers must explicitly pass `TimeProvider`. Removes the `?? TimeProvider.System` fallback trap. Update all call sites.

### D8: Widen `ExpectNoMsgAsync` windows

**Choice**: Increase `ExpectNoMsgAsync` durations proportionally — these assert "nothing should arrive" and scale with timefactor:
- `StreamConsumerActorSpec:236`: 100ms → 500ms
- `DiscoveryActorSpec:119`: 300ms → 500ms (already at 500ms on line 78)

Note: `ExpectNoMsgAsync` is inherently wall-clock-bound (it truly waits). With `timefactor = 3`, these multiply to 1.5s, which is acceptable.

## Risks / Trade-offs

- **Test duration increase**: `timefactor = 3` makes `ExpectNoMsgAsync` 3× slower. With current windows (100–500ms), the scaled waits (300ms–1.5s) are acceptable. Total suite impact is minor because most tests don't use `ExpectNoMsgAsync`.
  → Mitigation: Only widen windows that are currently too narrow, don't blanket-increase all timeouts.

- **`HistoryAnalyzer` breaking change**: Making `TimeProvider` non-nullable changes the method signature.
  → Mitigation: This is an internal API (not a NuGet package), and all callers are within the same solution. Update all call sites in one pass.

- **Custom `FakeTimeProvider` removal**: The enrichment shims might have subtle behavior differences.
  → Mitigation: The shims only override `GetUtcNow()` which the standard `FakeTimeProvider` handles identically. Run tests to verify.

## Open Questions

None — the approach is mechanical and well-understood.
