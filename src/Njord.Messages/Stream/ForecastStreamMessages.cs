using Akka.Streams;

namespace Njord.Messages.Stream;

public sealed record RequestForecastSink(long RequestId);

public abstract record RequestForecastSinkResponse(long RequestId);
public sealed record RequestForecastSinkCompleted(long RequestId, ISinkRef<DomainDataReceived> SinkRef)
    : RequestForecastSinkResponse(RequestId);
public sealed record RequestForecastSinkFailed(long RequestId, Exception Cause)
    : RequestForecastSinkResponse(RequestId);
