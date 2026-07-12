using Akka.Streams;

namespace Njord.Pipeline;

public sealed record HashResult(string Location, string ModelId, int Hash);

public sealed record Ack;

public sealed record RequestPipelineQueue;

public sealed record PipelineQueueResponse(ISourceQueueWithComplete<WeightedTarget> Queue);

public sealed record ScheduledPoll(string Location, string ModelId);
