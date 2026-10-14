using Njord.Weather;

namespace Njord.Messages.Pipeline;

public sealed record PollTarget(LocationId Location, string ModelId)
{
    public string Id => $"{Location.Name}|{ModelId}";

    public override string ToString() => $"{Location.Name}/{ModelId}";
}
