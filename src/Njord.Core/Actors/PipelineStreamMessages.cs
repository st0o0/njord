using Akka.Streams;
using Njord.Messages.Pipeline;

namespace Njord.Core.Actors;

public sealed record RequestPipelineOutput(long RequestId);

public abstract record RequestPipelineOutputResponse(long RequestId);
public sealed record RequestPipelineOutputCompleted(long RequestId, ISourceRef<PipelineEvent> SourceRef)
    : RequestPipelineOutputResponse(RequestId);
public sealed record RequestPipelineOutputFailed(long RequestId, Exception Cause)
    : RequestPipelineOutputResponse(RequestId);
