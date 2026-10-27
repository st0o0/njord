## Why

The gateway stream currently uses two independent token buckets for rate limiting: the built-in `Throttle` operator (using `DateTime.Now` internally via `TickTimeTokenBucket`) and the `GatewayBudgetGate` (using `TimeProvider`). These never synchronize -- the Throttle thinks it gave an element enough budget while the gate can still reject it. This dual-bucket architecture was introduced as a pragmatic fix for the MaxBurst problem, but it has inherent fragility:

- Two clocks: Throttle uses `DateTime.Now.Ticks`, gate uses `TimeProvider` -- tests with `FakeTimeProvider` only control the gate's clock, not the Throttle's.
- Two token states that can diverge after a rate refresh (gate refreshes every 5s, Throttle is static at materialization time).
- Budget rejections are still possible after the Throttle (the gate can reject even after the Throttle shaped the request).

A single custom `GraphStage` following the exact Akka.NET Throttle pattern (verified by decompiling `Akka.Streams.Implementation.Throttle<T>`) eliminates all three issues: one token bucket, one clock (`TimeProvider`), integrated accounting, and dynamic rate refresh.

## What Changes

- Add `BudgetGateStage<T>` as a custom `GraphStage<FlowShape<T, T>>` with `TimerGraphStageLogic` in `src/Njord.OpenMeteo/Budget/`
- Replace the `Throttle` + gate `Select` in `OpenMeteoGateway.OnSinkCompleted` with `.Via(new BudgetGateStage<ResolvedRequest>(...))`
- Remove `GatewayBudgetGate`, `IGatewayBudgetGate` -- their logic is absorbed into the stage
- Remove the `ResolvedRequest` record (the stage takes `FetchDomain` + cost function directly, or we keep it for client resolution)
- The stage never produces `FetchRejectionReason.Budget` -- elements are always parked until tokens are available

## Non-goals

- Changing `IBudgetProvider` or `BudgetRate` -- those remain as the rate source
- Changing `RecordApiCall` or the `BudgetTracker` -- accounting stays the same
- Adding admission deadlines or expiry logic -- elements park indefinitely (the PollWorker has its own reply timeout)
- Changing the `BudgetGuard` startup validator

## API-budget impact

No change to actual API call volume. Same rate, same accounting. Only the mechanism changes from dual-bucket (Throttle + Gate) to single-bucket (BudgetGateStage).

## Capabilities

### New Capabilities

(none)

### Modified Capabilities

- `dynamic-budget-throttle`: Replace the `Throttle` + `GatewayBudgetGate` requirements with a single `BudgetGateStage` requirement. Remove `IGatewayBudgetGate` requirement. The stage owns the token bucket, rate refresh, and accounting.
- `gateway-stream-dispatch`: Update stream topology from `Throttle -> gate Select` to `BudgetGateStage`. Remove budget rejection from the stream -- elements are delayed, not rejected.

## Impact

- `src/Njord.OpenMeteo/Budget/BudgetGateStage.cs` -- new file (~90 lines)
- `src/Njord.OpenMeteo/Budget/GatewayBudgetGate.cs` -- deleted
- `src/Njord.OpenMeteo/Budget/IGatewayBudgetGate.cs` -- deleted
- `src/Njord.OpenMeteo/Gateway/OpenMeteoGateway.cs` -- simplified stream (remove Throttle + gate Select, add Via BudgetGateStage)
- `src/Njord.OpenMeteo.Tests/GatewayBudgetGateTests.cs` -- replaced by BudgetGateStageTests
- `src/Njord.OpenMeteo.Tests/OpenMeteoGatewayAttachTests.cs` -- update budget tests
