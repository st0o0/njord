using Akka.Streams;
using Njord.Ingest;

namespace Njord.Pipeline;

public sealed record HashResult(string Location, string ModelId, int Hash);

public sealed record Ack;

public sealed record RequestPipelineSink;

public sealed record PipelineSinkResponse(ISinkRef<WeightedTarget> SinkRef);

public sealed record RequestPipelineSource;

public sealed record PipelineSourceResponse(ISourceRef<FetchOutcome> SourceRef);

public sealed record ScheduledPoll(string Location, string ModelId);

public sealed record FetchFailed(string Location, string ModelId, FetchFailureReason Reason, string Detail);

public sealed record TriggerImmediatePoll(string Location, string Model);

public sealed record TriggerPollResult(int Count, List<string> Targets);
