## 1. Pipeline Throttle

- [x] 1.1 Replace the weighted budget-percentage throttle in `src/Njord/Pipeline/PipelineActor.cs` with a fixed `Throttle(2, TimeSpan.FromSeconds(1), maximumBurst: 4, ThrottleMode.Shaping)`. Remove the `budget` and `budgetPerMinute` locals. Change `SelectAsyncUnordered(4)` to `SelectAsyncUnordered(2)`.

## 2. Scheduler Stagger Removal

- [x] 2.1 Simplify `InitializeStates()` in `src/Njord/Pipeline/SchedulerActor.cs`: remove the `staggerIndex` counter and the `staggerDelay` calculation. New (non-recovered) models get `NextPollUtc = now` instead of `now + staggerDelay`.

## 3. Test Updates

- [x] 3.1 Update `src/Njord.Tests/Pipeline/SchedulerActorSpec.cs`: remove or rewrite any tests that assert the `1 + i*2s` stagger timing. Add a test verifying that all initial polls are offered without stagger (all `NextPollUtc = now`).

## 4. Validation

- [x] 4.1 Run all tests: `dotnet run --project Njord.Tests/Njord.Tests.csproj` from `src/`.
- [x] 4.2 Run slopwatch: `dotnet slopwatch` from repo root.
