using Akka.Streams;
using Njord.Core.Egress;

namespace Njord.Core.Actors;

public sealed record RequestPipelineOutput(long RequestId);
public sealed record PipelineOutputResponse(long RequestId, ISourceRef<PipelineEvent> SourceRef);
public sealed record PipelineOutputFailed(long RequestId, Exception Cause);
