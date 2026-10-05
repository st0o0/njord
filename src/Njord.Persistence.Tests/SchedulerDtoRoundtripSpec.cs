using Njord.Tests.Shared;

namespace Njord.Persistence.Tests;

public sealed class SchedulerDtoRoundtripSpec
{
    [Fact]
    public void SchedulerSnapshotDto_roundtrip()
    {
        var dto = new SchedulerSnapshotDto
        {
            Version = 1,
            States = new Dictionary<string, ModelPollStateDto>
            {
                ["lucerne/icon_d2"] = new()
                {
                    LastHash = 42,
                    LastChangeUtcTicks = 638_600_000_000_000_000L,
                    PrevChangeUtcTicks = 638_599_000_000_000_000L,
                    NextPollUtcTicks = 638_601_000_000_000_000L,
                    MissCount = 2,
                    Phase = "Polling",
                    CycleTicks = 36_000_000_000L,
                },
            },
        };

        var result = PersistenceRoundtripHelper.Roundtrip(dto);

        Assert.Equal(dto.Version, result.Version);
        var state = Assert.Single(result.States);
        Assert.Equal("lucerne/icon_d2", state.Key);
        Assert.Equal(42, state.Value.LastHash);
        Assert.Equal(dto.States["lucerne/icon_d2"].LastChangeUtcTicks, state.Value.LastChangeUtcTicks);
        Assert.Equal(dto.States["lucerne/icon_d2"].PrevChangeUtcTicks, state.Value.PrevChangeUtcTicks);
        Assert.Equal(dto.States["lucerne/icon_d2"].NextPollUtcTicks, state.Value.NextPollUtcTicks);
        Assert.Equal(2, state.Value.MissCount);
        Assert.Equal("Polling", state.Value.Phase);
        Assert.Equal(36_000_000_000L, state.Value.CycleTicks);
    }

    [Fact]
    public void DataChangedDto_roundtrip()
    {
        var dto = new DataChangedDto
        {
            Version = 1,
            Location = "lucerne",
            ModelId = "icon_d2",
            Hash = 12345,
            UtcTicks = 638_600_000_000_000_000L,
        };

        var result = PersistenceRoundtripHelper.Roundtrip(dto);

        Assert.Equal(dto.Version, result.Version);
        Assert.Equal(dto.Location, result.Location);
        Assert.Equal(dto.ModelId, result.ModelId);
        Assert.Equal(dto.Hash, result.Hash);
        Assert.Equal(dto.UtcTicks, result.UtcTicks);
    }
}
