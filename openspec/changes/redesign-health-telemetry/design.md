## Context

Current: BackgroundService polls BudgetTracker actor every 5s via Ask, caches values as Volatile longs, observable gauges read them. Health checks Ask actors with 3s timeout. FunkArr: actors push, gauges read statics, health checks are synchronous pure functions.

## Goals / Non-Goals

**Goals:** Push-based telemetry, synchronous health checks, one Telemetry file per project.
**Non-Goals:** New metrics or health check logic.

## Decisions

### D1: Push pattern for gauges
```csharp
// In Telemetry.cs
internal static class Telemetry
{
    internal const string MeterName = "Njord.Budget";
    static readonly Meter Meter = new(MeterName);
    
    static long _dailyUsed, _monthlyUsed, _dailyLimit, _monthlyLimit;
    
    internal static void SetDailyUsed(long value) => Volatile.Write(ref _dailyUsed, value);
    internal static void SetMonthlyUsed(long value) => Volatile.Write(ref _monthlyUsed, value);
    
    // Observable gauges read these in their callbacks
    static readonly ObservableGauge<long> DailyUsed = Meter.CreateObservableGauge(
        "njord_budget_daily_used", () => Volatile.Read(ref _dailyUsed));
}
```
BudgetTracker calls `Telemetry.SetDailyUsed(state.DailyUsed)` in Apply.

### D2: State holder for health checks
```csharp
// DI-registered, thread-safe holder
public sealed class AtomicStateHolder<T> where T : class
{
    volatile T? _state;
    public T? Current => _state;
    public void Update(T state) => _state = state;
}
```
Actor writes: `_stateHolder.Update(state.ToHealth())`. Health check reads: `_stateHolder.Current`.

### D3: Health checks are pure Evaluate
```csharp
sealed class BudgetHealthCheck(AtomicStateHolder<BudgetUsage> holder) : IHealthCheck
{
    public Task<HealthCheckResult> CheckHealthAsync(...)
    {
        var state = holder.Current;
        return Task.FromResult(state is null
            ? HealthCheckResult.Degraded("No budget data yet")
            : Evaluate(state));
    }
    
    internal static HealthCheckResult Evaluate(BudgetUsage usage) { ... }
}
```

### D4: Delete BudgetGaugeRegistration
The entire BackgroundService, the Ask loop, the Volatile caching — all gone. Replaced by 3 lines in BudgetTracker's Apply.
