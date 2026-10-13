using Njord.Persistence.Events.Pipeline;

namespace Njord.Persistence.Tests;

public sealed class DataChangedRoundtripSpec
{
    [Fact]
    public void DataChanged_roundtrip()
    {
        var dto = new PersistedDataChanged
        {
            Location = "lucerne",
            ModelId = "icon_d2",
            Hash = 42,
            UtcTicks = 638_600_000_000_000_000L,
        };

        var result = PersistenceRoundtripHelper.Roundtrip(dto);

        Assert.Equal(dto.Location, result.Location);
        Assert.Equal(dto.ModelId, result.ModelId);
        Assert.Equal(dto.Hash, result.Hash);
        Assert.Equal(dto.UtcTicks, result.UtcTicks);
    }
}
