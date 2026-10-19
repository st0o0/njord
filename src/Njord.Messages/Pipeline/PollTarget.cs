using Njord.Model;

namespace Njord.Messages.Pipeline;

public sealed record PollTarget(Location Location)
{
    public string Id => Location.Name;

    public override string ToString() => Location.Name;
}
