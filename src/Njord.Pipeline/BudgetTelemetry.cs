namespace Njord.Pipeline;

public static class BudgetTelemetry
{
    public static void Observe(
        Func<long> usedDaily,
        Func<long> usedMonthly,
        Func<long> limitDaily,
        Func<long> limitMonthly)
    {
        Telemetry.Meter.CreateObservableGauge("njord_budget_used_daily", usedDaily, "{request}");
        Telemetry.Meter.CreateObservableGauge("njord_budget_used_monthly", usedMonthly, "{request}");
        Telemetry.Meter.CreateObservableGauge("njord_budget_limit_daily", limitDaily, "{request}");
        Telemetry.Meter.CreateObservableGauge("njord_budget_limit_monthly", limitMonthly, "{request}");
    }
}
