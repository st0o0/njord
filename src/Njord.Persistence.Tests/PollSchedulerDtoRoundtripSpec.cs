using Njord.Persistence.Events.Pipeline;

namespace Njord.Persistence.Tests;

public sealed class PollSchedulerDtoRoundtripSpec
{
    private static readonly DateTimeOffset LastChange = new(2026, 7, 15, 10, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset PrevChange = new(2026, 7, 15, 4, 0, 0, TimeSpan.Zero);

    [Fact]
    public void PersistedPollSchedulerState_roundtrip()
    {
        var dto = new PersistedPollSchedulerState(
        [
            new PersistedModelPollEntry(
                "Lucerne|icon_d2",
                42,
                LastChange,
                PrevChange,
                2,
                1,
                TimeSpan.FromHours(6),
                0),
            new PersistedModelPollEntry(
                "Lucerne|ecmwf_ifs025",
                null,
                null,
                null,
                0,
                0,
                null,
                1),
        ]);

        var result = PersistenceRoundtripHelper.Roundtrip(dto);

        Assert.Equal(2, result.Entries.Length);

        var first = result.Entries[0];
        Assert.Multiple(
            () => Assert.Equal("Lucerne|icon_d2", first.Key),
            () => Assert.Equal(42, first.LastHash),
            () => Assert.Equal(LastChange, first.LastChangeUtc),
            () => Assert.Equal(PrevChange, first.PrevChangeUtc),
            () => Assert.Equal(2, first.MissCount),
            () => Assert.Equal(1, first.Phase),
            () => Assert.Equal(TimeSpan.FromHours(6), first.Cycle),
            () => Assert.Equal(0, first.TransientFailureCount));

        var second = result.Entries[1];
        Assert.Multiple(
            () => Assert.Equal("Lucerne|ecmwf_ifs025", second.Key),
            () => Assert.Null(second.LastHash),
            () => Assert.Null(second.LastChangeUtc),
            () => Assert.Null(second.PrevChangeUtc),
            () => Assert.Equal(0, second.MissCount),
            () => Assert.Equal(0, second.Phase),
            () => Assert.Null(second.Cycle),
            () => Assert.Equal(1, second.TransientFailureCount));
    }
}
