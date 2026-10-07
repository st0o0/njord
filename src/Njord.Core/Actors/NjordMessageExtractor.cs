using Akka.Cluster.Sharding;
using Njord.Messages;

namespace Njord.Core.Actors;

public sealed class NjordMessageExtractor(int maxShards = 10) : HashCodeMessageExtractor(maxShards)
{
    public override string EntityId(object message) => message switch
    {
        IWithModelKey m => $"{m.Location}|{m.ModelId}",
        IWithEnrichmentKey m => $"{m.Location}|{m.TypeName}",
        IWithLocation m => m.Location,
        _ => throw new ArgumentException($"Unknown sharded message type: {message.GetType().Name}", nameof(message))
    };
}
