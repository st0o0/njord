namespace Njord.Messages.Pipeline;

public sealed record TriggerLocationPoll(string LocationName) : IWithLocationId;
