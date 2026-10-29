namespace Njord.Messages.Polling;

public sealed record StartPolling(PollTarget Target, TimeSpan DiscoveryInterval) : IWithLocationId
{
    public string LocationName => Target.Location.Name;
}
