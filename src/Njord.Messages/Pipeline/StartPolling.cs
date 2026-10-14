namespace Njord.Messages.Pipeline;

// Starts the target's worker, or updates its discovery interval when it is already polling.
public sealed record StartPolling(PollTarget Target, TimeSpan DiscoveryInterval) : IWithTargetId
{
    public string TargetId => Target.Id;
}
