namespace Njord.Persistence.Events.Pipeline;

public sealed record PersistedDataChanged(string Location, string ModelId, int Hash, long Ticks);
