using Akka.Streams;


namespace Njord.Messages.Stream;

public sealed record RequestPipelineOutput(long RequestId);

public abstract record RequestPipelineOutputResponse(long RequestId);
public sealed record RequestPipelineOutputCompleted(long RequestId, ISourceRef<PipelineEvent> SourceRef)
    : RequestPipelineOutputResponse(RequestId);
public sealed record RequestPipelineOutputFailed(long RequestId, Exception Cause)
    : RequestPipelineOutputResponse(RequestId);
