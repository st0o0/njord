using Akka.Streams;
using Njord.Ingest;

namespace Njord.Pipeline;

public sealed record HashResult(string Location, string ModelId, int Hash);

public sealed record Ack;

public sealed record RequestPipelineSink;

public sealed record PipelineSinkResponse(ISinkRef<WeightedTarget> SinkRef);

public sealed record RequestPipelineSource;

public sealed record PipelineSourceResponse(ISourceRef<FetchOutcome.Success> SourceRef);

public sealed record ScheduledPoll(string Location, string ModelId);
