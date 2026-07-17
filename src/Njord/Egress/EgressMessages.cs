using Akka.Streams;

namespace Njord.Egress;

public sealed record RequestEgressSink;
public sealed record EgressSinkResponse(ISinkRef<EgressEvent> SinkRef);

public sealed record RequestEgressSource;
public sealed record EgressSourceResponse(ISourceRef<EgressEvent> SourceRef);
