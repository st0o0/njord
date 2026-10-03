using Akka.Streams;

namespace Njord.Messages.Egress;

public sealed record RequestEgressSink(long RequestId);
public sealed record EgressSinkResponse(long RequestId, ISinkRef<EgressEvent> SinkRef);

public sealed record RequestEgressSource(long RequestId);
public sealed record EgressSourceResponse(long RequestId, ISourceRef<EgressEvent> SourceRef);
public sealed record EgressSinkFailed(long RequestId, Exception Cause);
public sealed record EgressSourceFailed(long RequestId, Exception Cause);
