## Why

The `GatewayBudgetGate` token bucket has a MaxBurst of 16, but a single Weather fetch with 16 ForecastDays and 3 models costs 18 tokens (CallWeight = ceil(23/10) * ceil(16/14) * 3 = 18). This makes it mathematically impossible to acquire — every Weather fetch is immediately rejected with `FetchRejectionReason.Budget` regardless of available budget. The PollWorker then waits at least 5 minutes (budget rejection backoff) before retrying, causing unnecessary delays on every poll cycle. Proven in E2E with 3 locations, 16 ForecastDays, 3 models: all 3 Weather fetches rejected at startup while lighter domains (weight=1) pass through fine.

## What Changes

- Add a native Akka.Streams `Throttle` stage with `ThrottleMode.Shaping` and cost-based weighting before the budget gate `Select` in the gateway stream. This applies backpressure (delay) instead of hard rejection for rate limiting.
- Fix `OptionsBudgetProvider.MaxBurst` calculation to be at least `max(client.CallWeight)` across all registered domain clients, so a single high-weight fetch can always be admitted when the bucket is full.
- Simplify `GatewayBudgetGate` to an accounting and safety-net layer — the `Throttle` stage handles rate shaping, the gate handles usage tracking and edge-case overflow protection.
- Remove `EstimateDelay` from `IGatewayBudgetGate` — the native `Throttle` stage handles delay estimation internally.

## Non-goals

- Changing the `BudgetGuard` startup validator — it checks projected monthly usage, not runtime burst capacity.
- Changing `PollWorker` retry logic or `FailureBackoff` — those remain as fallback for genuine failures.
- Replacing the `GatewayBudgetGate` entirely — it still owns token accounting and `RecordApiCall`.
- Adding admission deadlines or the `GatewayThrottleStage` custom GraphStage described in the existing `dynamic-budget-throttle` spec — that is a more sophisticated future evolution; this change uses the built-in `Throttle` operator.

## API-budget impact

No change to actual API call volume or polling frequency. This change only affects how requests are paced within the gateway stream. The same number of requests reach Open-Meteo; they are delayed by milliseconds instead of rejected and retried after 5 minutes.

## Capabilities

### New Capabilities

(none)

### Modified Capabilities

- `dynamic-budget-throttle`: Add the native `Throttle` stage requirement and update `MaxBurst` floor to `max(client.CallWeight)`. The existing `GatewayThrottleStage` custom GraphStage requirement becomes a future evolution, not the current implementation.
- `gateway-stream-dispatch`: Update the stream topology to include the `Throttle` stage between `Source.Queue` and the budget gate `Select`.

## Impact

- `src/Njord.OpenMeteo/Gateway/OpenMeteoGateway.cs` — stream topology change (add `Throttle` before gate)
- `src/Njord.OpenMeteo/Budget/OptionsBudgetProvider.cs` — MaxBurst floor calculation
- `src/Njord.OpenMeteo/Budget/IGatewayBudgetGate.cs` — remove `EstimateDelay`
- `src/Njord.OpenMeteo/Budget/GatewayBudgetGate.cs` — remove `EstimateDelay`, adjust MaxBurst
- `src/Njord.OpenMeteo.Tests/GatewayBudgetGateTests.cs` — update for removed method
- `src/Njord.OpenMeteo.Tests/` — new test for throttle behavior in gateway stream
