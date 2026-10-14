using Akka.Cluster.Sharding;
using Njord.Messages.Pipeline;

namespace Njord.Core.Actors;

public sealed class ShardMessageExtractor(int maxShards = 5) : HashCodeMessageExtractor(maxShards)
{
    public override string EntityId(object message) => message switch
    {
        IWithTargetId m => m.TargetId,
        _ => throw new ArgumentException($"Unknown sharded message type: {message.GetType().Name}", nameof(message)),
    };
}
