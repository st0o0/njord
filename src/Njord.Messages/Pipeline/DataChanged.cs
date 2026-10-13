using Njord.Weather;

namespace Njord.Messages.Pipeline;

public sealed record DataChanged(LocationId Location, string ModelId, int Hash, DateTimeOffset Utc);
