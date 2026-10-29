using Njord.Core;

namespace Njord.Messages.Polling;

public sealed record PollTarget(Location Location)
{
    public string Id => Location.Name;

    public override string ToString() => Location.Name;
}
