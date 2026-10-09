namespace Njord.Messages.Pipeline;

public sealed record DataChanged(string Location, string ModelId, int Hash, DateTimeOffset Utc);
