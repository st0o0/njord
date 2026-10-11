using Njord.Messages.Pipeline;
using Njord.Persistence;

namespace Njord.Pipeline;

public static class SchedulerDtoMapping
{
    public static DataChangedDto ToDto(DataChanged evt) => new()
    {
        Location = evt.Location,
        ModelId = evt.ModelId,
        Hash = evt.Hash,
        UtcTicks = evt.Utc.UtcTicks,
    };

    public static DataChanged ToDomain(DataChangedDto dto) =>
        new(dto.Location, dto.ModelId, dto.Hash, new DateTimeOffset(dto.UtcTicks, TimeSpan.Zero));
}
