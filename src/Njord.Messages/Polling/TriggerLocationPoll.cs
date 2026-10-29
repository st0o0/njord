namespace Njord.Messages.Polling;

public sealed record TriggerLocationPoll(string LocationName) : IWithLocationId;
