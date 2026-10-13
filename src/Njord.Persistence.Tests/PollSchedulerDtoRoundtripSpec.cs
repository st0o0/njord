using Njord.Persistence.Events.Pipeline;

namespace Njord.Persistence.Tests;

public sealed class PollSchedulerDtoRoundtripSpec
{
    [Fact]
    public void PersistedPollSchedulerState_roundtrip()
    {
        var dto = new PersistedPollSchedulerState
        {
            Entries =
            [
                new PersistedModelPollEntry
                {
                    Key = "Lucerne|icon_d2",
                    LastHash = 42,
                    LastChangeUtcTicks = 638_600_000_000_000_000L,
                    PrevChangeUtcTicks = 638_599_000_000_000_000L,
                    MissCount = 2,
                    Phase = 1,
                    CycleTicks = TimeSpan.FromHours(6).Ticks,
                    TransientFailureCount = 0,
                },
                new PersistedModelPollEntry
                {
                    Key = "Lucerne|ecmwf_ifs025",
                    LastHash = null,
                    LastChangeUtcTicks = null,
                    PrevChangeUtcTicks = null,
                    MissCount = 0,
                    Phase = 0,
                    CycleTicks = null,
                    TransientFailureCount = 1,
                },
            ],
        };

        var result = PersistenceRoundtripHelper.Roundtrip(dto);

        Assert.Equal(2, result.Entries.Length);

        var first = result.Entries[0];
        Assert.Multiple(
            () => Assert.Equal("Lucerne|icon_d2", first.Key),
            () => Assert.Equal(42, first.LastHash),
            () => Assert.Equal(638_600_000_000_000_000L, first.LastChangeUtcTicks),
            () => Assert.Equal(638_599_000_000_000_000L, first.PrevChangeUtcTicks),
            () => Assert.Equal(2, first.MissCount),
            () => Assert.Equal(1, first.Phase),
            () => Assert.Equal(TimeSpan.FromHours(6).Ticks, first.CycleTicks),
            () => Assert.Equal(0, first.TransientFailureCount));

        var second = result.Entries[1];
        Assert.Multiple(
            () => Assert.Equal("Lucerne|ecmwf_ifs025", second.Key),
            () => Assert.Null(second.LastHash),
            () => Assert.Null(second.LastChangeUtcTicks),
            () => Assert.Null(second.PrevChangeUtcTicks),
            () => Assert.Equal(0, second.MissCount),
            () => Assert.Equal(0, second.Phase),
            () => Assert.Null(second.CycleTicks),
            () => Assert.Equal(1, second.TransientFailureCount));
    }
}
