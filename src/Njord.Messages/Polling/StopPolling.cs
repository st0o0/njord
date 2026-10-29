namespace Njord.Messages.Polling;

public sealed record StopPolling(string LocationName) : IWithLocationId;
