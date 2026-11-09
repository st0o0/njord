using Akka.Streams;
using Njord.Grpc.V3;

namespace Njord.Grpc;

public sealed record RequestEntityStream(long RequestId);

public abstract record RequestEntityStreamResponse(long RequestId);

public sealed record RequestEntityStreamCompleted(long RequestId, ISourceRef<EntityEvent> SourceRef)
    : RequestEntityStreamResponse(RequestId);

public sealed record RequestEntityStreamFailed(long RequestId, Exception Cause)
    : RequestEntityStreamResponse(RequestId);
