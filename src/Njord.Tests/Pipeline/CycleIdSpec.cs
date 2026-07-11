using Microsoft.Extensions.Time.Testing;
using Njord.Domain;

namespace Njord.Tests.Pipeline;

public sealed class CycleIdSpec
{
    [Fact(Timeout = 5000)]
    public void Cycle_ids_derive_from_the_injected_clock()
    {
        var tick = new DateTimeOffset(2026, 7, 11, 12, 0, 0, TimeSpan.Zero);

        var cycle = CycleId.From(new FakeTimeProvider(tick));

        Assert.Equal(tick, cycle.Timestamp);
    }
}
