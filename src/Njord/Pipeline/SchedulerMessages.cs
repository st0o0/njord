using Akka.Streams;
using Njord.Domain.Weather;

namespace Njord.Pipeline;

public sealed record HashResult(string Location, string ModelId, int Hash);

public sealed record Ack;

public sealed record RequestPipelineSink(long RequestId);

public sealed record PipelineSinkResponse(long RequestId, ISinkRef<WeightedTarget> SinkRef);

public sealed record RequestPipelineSource(long RequestId);

public sealed record PipelineSourceResponse(long RequestId, ISourceRef<FetchOutcome> SourceRef);

public sealed record ScheduledPoll(string Location, string ModelId);

public sealed record FetchFailed(string Location, string ModelId, FetchFailureReason Reason, string Detail);

public sealed record TriggerImmediatePoll(string Location, string Model);

public sealed record TriggerPollResult(int Count, List<string> Targets);

public sealed record GetPollStates;

public sealed record PollStateEntry(
    string Location,
    string ModelId,
    PollPhase Phase,
    DateTimeOffset NextPollUtc,
    DateTimeOffset? LastChangeUtc,
    int MissCount,
    long? CycleSeconds);

public sealed record PollStatesSnapshot(IReadOnlyList<PollStateEntry> Entries);
