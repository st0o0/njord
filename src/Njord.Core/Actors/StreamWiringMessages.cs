using Akka.Streams;
using Njord.Messages.Pipeline;
using Njord.Core.Domain;

namespace Njord.Core.Actors;

public sealed record RequestPipelineOutput(long RequestId);

public abstract record RequestPipelineOutputResponse(long RequestId);
public sealed record RequestPipelineOutputCompleted(long RequestId, ISourceRef<PipelineEvent> SourceRef)
    : RequestPipelineOutputResponse(RequestId);
public sealed record RequestPipelineOutputFailed(long RequestId, Exception Cause)
    : RequestPipelineOutputResponse(RequestId);

public sealed record RequestForecastSink(long RequestId);

public abstract record RequestForecastSinkResponse(long RequestId);
public sealed record RequestForecastSinkCompleted(long RequestId, ISinkRef<DomainDataReceived> SinkRef)
    : RequestForecastSinkResponse(RequestId);
public sealed record RequestForecastSinkFailed(long RequestId, Exception Cause)
    : RequestForecastSinkResponse(RequestId);

public sealed record RequestGrpcStream(long RequestId);

public abstract record RequestGrpcStreamResponse(long RequestId);
public sealed record RequestGrpcStreamCompleted(long RequestId, ISourceRef<PipelineEvent> SourceRef)
    : RequestGrpcStreamResponse(RequestId);
public sealed record RequestGrpcStreamFailed(long RequestId, Exception Cause)
    : RequestGrpcStreamResponse(RequestId);
