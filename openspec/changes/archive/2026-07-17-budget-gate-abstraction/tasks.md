## 1. IBudgetGate Interface and WeightedBudgetGate

- [x] 1.1 Create `src/Njord/Pipeline/IBudgetGate.cs` with `IBudgetGate<T>` interface (`AcquireAsync(T, CancellationToken)`) and `WeightedBudgetGate` implementation containing the token-bucket logic (moved from the stage), `IBudgetProvider` polling, `BudgetTracker.RecordCall()`, and cost extraction via `WeightedTarget.Weight`.

## 2. Simplify BudgetThrottleStage

- [x] 2.1 Rewrite `src/Njord/Pipeline/BudgetThrottleStage.cs`: single `IBudgetGate<T>` constructor parameter. Logic: `OnPush` → `AcquireAsync` + `GetAsyncCallback` → `Push`. No timers, no token bucket, no provider, no tracker.

## 3. Update PipelineActor

- [x] 3.1 Update `src/Njord/Pipeline/PipelineActor.cs`: create `WeightedBudgetGate(budgetProvider, budgetTracker)`, pass to `BudgetThrottleStage`. Remove direct `IBudgetProvider` and `BudgetTracker` fields — they flow through the gate.

## 4. Tests

- [x] 4.1 Rewrite `src/Njord.Tests/Pipeline/BudgetThrottleStageSpec.cs`: use a `FakeBudgetGate` (instant or delayed) instead of `FakeBudgetProvider`. Test stage behaviour (pass-through, delay, upstream-finish). Add a separate `WeightedBudgetGateSpec` for gate-level tests (token bucket, rate change, RecordCall).

## 5. Validation

- [x] 5.1 Run all tests: `dotnet run --project Njord.Tests/Njord.Tests.csproj` from `src/`.
- [x] 5.2 Run slopwatch: `dotnet slopwatch` from repo root.
