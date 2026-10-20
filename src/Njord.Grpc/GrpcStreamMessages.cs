using Akka.Streams;
using Njord.Messages.Pipeline;

namespace Njord.Grpc;

public sealed record RequestGrpcStream(long RequestId);

public abstract record RequestGrpcStreamResponse(long RequestId);
public sealed record RequestGrpcStreamCompleted(long RequestId, ISourceRef<PipelineEvent> SourceRef)
    : RequestGrpcStreamResponse(RequestId);
public sealed record RequestGrpcStreamFailed(long RequestId, Exception Cause)
    : RequestGrpcStreamResponse(RequestId);
