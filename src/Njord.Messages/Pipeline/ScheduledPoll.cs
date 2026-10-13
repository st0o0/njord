using Njord.Weather;

namespace Njord.Messages.Pipeline;

public sealed record ScheduledPoll(LocationId Location, string ModelId);
