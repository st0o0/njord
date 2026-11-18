namespace Njord.Messages.Polling;

public sealed record StartPolling(PollTarget Target, Dictionary<int, TimeSpan> DomainIntervals) : IWithLocationId
{
    public string LocationName => Target.Location.Name;
}
