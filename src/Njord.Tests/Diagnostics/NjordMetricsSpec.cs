using System.Diagnostics.Metrics;
using Njord.Diagnostics;

namespace Njord.Tests.Diagnostics;

public sealed class NjordMetricsSpec
{
    [Fact(Timeout = 5000)]
    public void Instance_returns_same_reference()
    {
        var a = NjordMetrics.Instance;
        var b = NjordMetrics.Instance;

        Assert.Same(a, b);
    }

    [Fact(Timeout = 5000)]
    public void Meter_is_named_Njord()
    {
        Assert.Equal("Njord", NjordMetrics.Instance.Meter.Name);
    }

    [Fact(Timeout = 5000)]
    public void Extension_creates_counter_on_shared_meter()
    {
        var counter = NjordMetrics.Instance.AddFetchTotal();

        Assert.Equal("njord_fetch_total", counter.Name);
        Assert.Equal(NjordMetrics.Instance.Meter, counter.Meter);
    }
}
