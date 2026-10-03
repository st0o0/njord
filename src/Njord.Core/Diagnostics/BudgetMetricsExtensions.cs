using System.Diagnostics.Metrics;

namespace Njord.Diagnostics;

public static class BudgetMetricsExtensions
{
    public static ObservableGauge<long> AddBudgetUsedDaily(this NjordMetrics m, Func<long> observe) =>
        m.Meter.CreateObservableGauge("njord_budget_used_daily", observe, "{request}");

    public static ObservableGauge<long> AddBudgetUsedMonthly(this NjordMetrics m, Func<long> observe) =>
        m.Meter.CreateObservableGauge("njord_budget_used_monthly", observe, "{request}");

    public static ObservableGauge<long> AddBudgetLimitDaily(this NjordMetrics m, Func<long> observe) =>
        m.Meter.CreateObservableGauge("njord_budget_limit_daily", observe, "{request}");

    public static ObservableGauge<long> AddBudgetLimitMonthly(this NjordMetrics m, Func<long> observe) =>
        m.Meter.CreateObservableGauge("njord_budget_limit_monthly", observe, "{request}");

    public static Histogram<double> AddThrottleWait(this NjordMetrics m) =>
        m.Meter.CreateHistogram<double>("njord_throttle_wait_seconds", "s");
}
