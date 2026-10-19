namespace Njord.Messages.Pipeline;

public sealed record StopPolling(string LocationName) : IWithLocationId;
