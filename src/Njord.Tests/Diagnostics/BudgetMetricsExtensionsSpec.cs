using System.Diagnostics.Metrics;
using Njord.Diagnostics;

namespace Njord.Tests.Diagnostics;

public sealed class BudgetMetricsExtensionsSpec
{
    [Fact]
    public void AddBudgetUsedDaily_reads_from_callback()
    {
        long value = 42;
        var gauge = NjordMetrics.Instance.AddBudgetUsedDaily(() => value);

        Assert.Equal("njord_budget_used_daily", gauge.Name);
        Assert.Equal("{request}", gauge.Unit);
    }

    [Fact]
    public void AddBudgetLimitMonthly_reads_from_callback()
    {
        long value = 300_000;
        var gauge = NjordMetrics.Instance.AddBudgetLimitMonthly(() => value);

        Assert.Equal("njord_budget_limit_monthly", gauge.Name);
    }

    [Fact]
    public void AddThrottleWait_creates_histogram()
    {
        var histogram = NjordMetrics.Instance.AddThrottleWait();

        Assert.Equal("njord_throttle_wait_seconds", histogram.Name);
        Assert.Equal("s", histogram.Unit);
    }
}
