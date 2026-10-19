using Njord.Model;

namespace Njord.Messages.Pipeline;

public sealed record PollTarget(LocationId Location)
{
    public string Id => Location.Name;

    public override string ToString() => Location.Name;
}
