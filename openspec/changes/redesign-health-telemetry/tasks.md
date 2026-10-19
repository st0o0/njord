## 1. AtomicStateHolder

- [ ] 1.1 Add `AtomicStateHolder<T>` to `src/Njord.Core/` (generic, thread-safe)
- [ ] 1.2 Tests for AtomicStateHolder

## 2. Budget telemetry push pattern

- [ ] 2.1 Merge `BudgetTelemetry.cs` into `Telemetry.cs` in Njord.Budget
- [ ] 2.2 Add static Set methods for daily/monthly used and limits
- [ ] 2.3 BudgetTracker: call `Telemetry.SetX()` in Apply after state change
- [ ] 2.4 Store budget limits (from ConfigView) in Telemetry statics at startup
- [ ] 2.5 Delete `BudgetGaugeRegistration` BackgroundService
- [ ] 2.6 Delete registration from `BudgetSetupContainer`
- [ ] 2.7 Update tests

## 3. Health checks — synchronous state reads

- [ ] 3.1 Register `AtomicStateHolder<BudgetUsage>` in DI
- [ ] 3.2 BudgetTracker writes to holder on state change
- [ ] 3.3 Rewrite `BudgetHealthCheck` to read from holder (no Ask)
- [ ] 3.4 Add `static Evaluate(BudgetUsage)` pure method + tests
- [ ] 3.5 Repeat for PollHealthCheck (AtomicStateHolder<PollHealth>)
- [ ] 3.6 Repeat for MqttConnectionHealthCheck (AtomicStateHolder<MqttConnectionStatus>)

## 4. Pipeline/Mqtt telemetry alignment

- [ ] 4.1 Verify Pipeline Telemetry.cs is one file with push pattern
- [ ] 4.2 Verify Mqtt Telemetry.cs is one file with push pattern
- [ ] 4.3 Merge any secondary telemetry files

## 5. Validation

- [ ] 5.1 `dotnet build Njord.slnx`
- [ ] 5.2 All test projects pass
- [ ] 5.3 No BackgroundService for telemetry remains
- [ ] 5.4 No actor Ask in health checks remains
