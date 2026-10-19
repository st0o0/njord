## 1. AtomicStateHolder

- [x] 1.1 Add `AtomicStateHolder<T>` to `src/Njord.Core/` (generic, thread-safe)
- [x] 1.2 Tests for AtomicStateHolder

## 2. Budget telemetry push pattern

- [x] 2.1 Merge `BudgetTelemetry.cs` into `Telemetry.cs` in Njord.Budget
- [x] 2.2 Add static Set methods for daily/monthly used and limits
- [x] 2.3 BudgetTracker: call `Telemetry.SetX()` in Apply after state change
- [x] 2.4 Store budget limits (from ConfigView) in Telemetry statics at startup
- [x] 2.5 Delete `BudgetGaugeRegistration` BackgroundService
- [x] 2.6 Delete registration from `BudgetSetupContainer`
- [x] 2.7 Update tests

## 3. Health checks — synchronous state reads

- [x] 3.1 Register `AtomicStateHolder<BudgetUsage>` in DI
- [x] 3.2 BudgetTracker writes to holder on state change
- [x] 3.3 Rewrite `BudgetHealthCheck` to read from holder (no Ask)
- [x] 3.4 Add `static Evaluate(BudgetUsage)` pure method + tests
- [x] 3.5 Repeat for PollHealthCheck (AtomicStateHolder<PollHealth>)
- [x] 3.6 Repeat for MqttConnectionHealthCheck (AtomicStateHolder<MqttConnectionStatus>)

## 4. Pipeline/Mqtt telemetry alignment

- [x] 4.1 Verify Pipeline Telemetry.cs is one file with push pattern
- [x] 4.2 Verify Mqtt Telemetry.cs is one file with push pattern
- [x] 4.3 Merge any secondary telemetry files

## 5. Validation

- [x] 5.1 `dotnet build Njord.slnx`
- [x] 5.2 All test projects pass
- [x] 5.3 No BackgroundService for telemetry remains
- [x] 5.4 No actor Ask in health checks remains
