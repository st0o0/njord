namespace Njord.Messages.Polling;

// Starts the target's worker, or updates its discovery interval when it is already polling.
public sealed record StartPolling(PollTarget Target, TimeSpan DiscoveryInterval) : IWithLocationId
{
    public string LocationName => Target.Location.Name;
}
