## 1. IBudgetProvider

- [x] 1.1 Create `src/Njord/Pipeline/IBudgetProvider.cs` with `IBudgetProvider` interface (`GetCurrentRate()` → `BudgetRate`), `BudgetRate` record, and `OptionsBudgetProvider` implementation reading from `IOptionsMonitor<NjordOptions>`.
- [x] 1.2 Register `IBudgetProvider` as singleton in `src/Njord/Configuration/NjordServiceSetup.cs`.

## 2. BudgetThrottleStage

- [x] 2.1 Create `src/Njord/Pipeline/BudgetThrottleStage.cs` — a generic `GraphStage<FlowShape<T, T>>` with weighted token-bucket logic, periodic `IBudgetProvider` polling (every ~5 sec via scheduled timer), and `BudgetTracker.RecordCall(weight)` on each pass-through.

## 3. Wire into Pipeline

- [x] 3.1 Update `src/Njord/Pipeline/PipelineActor.cs`: inject `IBudgetProvider` and `BudgetTracker`. Replace `.Throttle(2, 1sec, burst: 4)` with `.Via(new BudgetThrottleStage<WeightedTarget>(provider, tracker, t => t.Weight))`.

## 4. Tests

- [x] 4.1 Create `src/Njord.Tests/Pipeline/BudgetThrottleStageSpec.cs`: test elements pass at budget rate, weighted elements consume proportional tokens, rate change takes effect, `BudgetTracker.RecordCall` is invoked.

## 5. Validation

- [x] 5.1 Run all tests: `dotnet run --project Njord.Tests/Njord.Tests.csproj` from `src/`.
- [x] 5.2 Run slopwatch: `dotnet slopwatch` from repo root.
