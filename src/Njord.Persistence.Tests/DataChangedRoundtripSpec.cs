using Njord.Persistence.Events.Pipeline;

namespace Njord.Persistence.Tests;

public sealed class DataChangedRoundtripSpec
{
    [Fact]
    public void DataChanged_roundtrip()
    {
        var dto = new PersistedDataChanged(
            "lucerne", "icon_d2", 42, new DateTimeOffset(2024, 7, 15, 10, 0, 0, TimeSpan.Zero));

        var result = PersistenceRoundtripHelper.Roundtrip(dto);

        Assert.Equal(dto.Location, result.Location);
        Assert.Equal(dto.ModelId, result.ModelId);
        Assert.Equal(dto.Hash, result.Hash);
        Assert.Equal(dto.Utc, result.Utc);
    }
}
