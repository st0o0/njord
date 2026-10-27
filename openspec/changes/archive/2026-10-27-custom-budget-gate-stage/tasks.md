## 1. BudgetGateStage implementation

- [x] 1.1 Create `BudgetGateStage<T>` in `src/Njord.OpenMeteo/Budget/BudgetGateStage.cs`. Internal sealed class extending `GraphStage<FlowShape<T, T>>` with a nested `Logic` class extending `TimerGraphStageLogic` implementing `IInHandler` and `IOutHandler`. Constructor takes `IBudgetProvider`, `IActorRef` tracker, `TimeProvider`, `Func<T, int>` cost function, `int maxCallWeight`. Logic: token bucket with `Refill()` using `TimeProvider`, `OnPush` tries acquire or parks with `ScheduleOnce`, `OnPull` always `Pull(In)`, `OnTimer("retry")` refills and re-checks, `OnTimer("refresh")` updates rate from provider every 5s via `ScheduleRepeatedly` in `PreStart`. `OnUpstreamFinish` drains parked element via `_willStop` flag.

## 2. BudgetGateStage tests

- [x] 2.1 Create `BudgetGateStageTests` in `src/Njord.OpenMeteo.Tests/BudgetGateStageTests.cs` using Akka.Hosting.TestKit. Tests with high rate (6000/min) for short real-time delays: (a) element passes immediately when tokens available, (b) parked element is released after delay, (c) two consecutive heavy elements are serialized (not rejected), (d) rate refresh updates the token rate, (e) upstream completion drains parked element.

## 3. Replace Throttle + Gate in gateway

- [x] 3.1 In `OpenMeteoGateway.OnSinkCompleted` (`src/Njord.OpenMeteo/Gateway/OpenMeteoGateway.cs`): remove the `.Throttle(...)` call and the gate `.Select(resolved => gate.TryAcquire...)` block. Replace with `.Via(new BudgetGateStage<ResolvedRequest>(budgetProvider, trackerActor, timeProvider, r => r.Client?.CallWeight ?? 1, maxCallWeight))` followed by a `.Select(resolved => resolved.Client is null ? PipelineUnavailable : admitted)`. Remove `_gate` field, `_budgetProvider` field (pass directly to stage), `_maxCallWeight` field. Remove `IGatewayBudgetGate` import.
- [x] 3.2 Update `OpenMeteoGatewayAttachTests` (`src/Njord.OpenMeteo.Tests/OpenMeteoGatewayAttachTests.cs`): update `A_high_weight_request_succeeds_when_burst_is_raised_to_call_weight` and `Throttle_shapes_consecutive_heavy_requests_without_rejection` to work with the new stage. Remove any references to `GatewayBudgetGate`.

## 4. Remove old gate classes

- [x] 4.1 Delete `src/Njord.OpenMeteo/Budget/GatewayBudgetGate.cs` and `src/Njord.OpenMeteo/Budget/IGatewayBudgetGate.cs`.
- [x] 4.2 Delete or replace `src/Njord.OpenMeteo.Tests/GatewayBudgetGateTests.cs` (functionality covered by BudgetGateStageTests).

## 5. Validation

- [x] 5.1 Run all test projects to verify no regressions:
  ```
  dotnet build Njord.slnx
  dotnet run --project Njord.OpenMeteo.Tests/Njord.OpenMeteo.Tests.csproj
  dotnet run --project Njord.Grpc.Tests/Njord.Grpc.Tests.csproj
  dotnet run --project Njord.Core.Tests/Njord.Core.Tests.csproj
  dotnet run --project Njord.Pipeline.Tests/Njord.Pipeline.Tests.csproj
  dotnet run --project Njord.Features.Tests/Njord.Features.Tests.csproj
  dotnet run --project Njord.Budget.Tests/Njord.Budget.Tests.csproj
  dotnet run --project Njord.Polling.Tests/Njord.Polling.Tests.csproj
  dotnet run --project Njord.Persistence.Tests/Njord.Persistence.Tests.csproj
  dotnet run --project Njord.Messages.Tests/Njord.Messages.Tests.csproj
  dotnet run --project Njord.Forecast.Tests/Njord.Forecast.Tests.csproj
  dotnet run --project Njord.IntegrationTests/Njord.IntegrationTests.csproj
  dotnet run --project Njord.Architecture.Tests/Njord.Architecture.Tests.csproj
  dotnet format Njord.slnx --verify-no-changes
  ```
