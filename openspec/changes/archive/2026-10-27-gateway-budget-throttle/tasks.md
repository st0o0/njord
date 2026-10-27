## 1. MaxBurst floor fix

- [x] 1.1 Add `maxCallWeight` constructor parameter to `GatewayBudgetGate` (`src/Njord.OpenMeteo/Budget/GatewayBudgetGate.cs`). Change `ApplyRate` to compute `_maxBurst = Math.Max(rate.MaxBurst, maxCallWeight)`. Pass `_clients.Values.Max(c => c.CallWeight)` from `OpenMeteoGateway` constructor.
- [x] 1.2 Update `OptionsBudgetProvider` (`src/Njord.OpenMeteo/Budget/OptionsBudgetProvider.cs`) — keep `BudgetRate.MaxBurst` as the base value (the gate applies the floor). No change needed here if the gate handles the floor.
- [x] 1.3 Update `GatewayBudgetGateTests` (`src/Njord.OpenMeteo.Tests/GatewayBudgetGateTests.cs`) — add test: when `maxCallWeight=18` and base burst=16, a `TryAcquire(18)` succeeds on a full bucket.

## 2. Remove EstimateDelay

- [x] 2.1 Remove `EstimateDelay` from `IGatewayBudgetGate` (`src/Njord.OpenMeteo/Budget/IGatewayBudgetGate.cs`) and its implementation in `GatewayBudgetGate`.
- [x] 2.2 Remove `EstimateDelay` tests from `GatewayBudgetGateTests` (`src/Njord.OpenMeteo.Tests/GatewayBudgetGateTests.cs`).

## 3. Add Throttle stage to gateway stream

- [x] 3.1 In `OpenMeteoGateway.OnSinkCompleted` (`src/Njord.OpenMeteo/Gateway/OpenMeteoGateway.cs`), restructure the stream: after `Source.Queue`, add a `Select` to resolve the domain client, then `.Throttle(costPerMinute, TimeSpan.FromMinutes(1), maxBurst, gated => gated.client?.CallWeight ?? 1, ThrottleMode.Shaping)`, then the existing gate `Select`. Extract `costPerMinute` and `maxBurst` from `_gate` or from the budget provider at materialization time.
- [x] 3.2 Add integration-style test in `src/Njord.OpenMeteo.Tests/` verifying that a high-weight fetch (weight > base burst) is delayed and eventually emitted rather than rejected, when using the throttled gateway stream.

## 4. Validation

- [x] 4.1 Run all test projects to verify no regressions:
  ```
  dotnet build Njord.slnx
  dotnet run --project Njord.OpenMeteo.Tests/Njord.OpenMeteo.Tests.csproj
  dotnet run --project Njord.Core.Tests/Njord.Core.Tests.csproj
  dotnet run --project Njord.Polling.Tests/Njord.Polling.Tests.csproj
  dotnet run --project Njord.Grpc.Tests/Njord.Grpc.Tests.csproj
  dotnet run --project Njord.Pipeline.Tests/Njord.Pipeline.Tests.csproj
  dotnet run --project Njord.Features.Tests/Njord.Features.Tests.csproj
  dotnet run --project Njord.Budget.Tests/Njord.Budget.Tests.csproj
  dotnet run --project Njord.Persistence.Tests/Njord.Persistence.Tests.csproj
  dotnet run --project Njord.Messages.Tests/Njord.Messages.Tests.csproj
  dotnet run --project Njord.Forecast.Tests/Njord.Forecast.Tests.csproj
  dotnet run --project Njord.IntegrationTests/Njord.IntegrationTests.csproj
  dotnet run --project Njord.Architecture.Tests/Njord.Architecture.Tests.csproj
  dotnet format Njord.slnx --verify-no-changes
  ```
