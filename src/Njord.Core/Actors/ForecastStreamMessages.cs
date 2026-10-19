using Akka.Streams;
using Njord.Core.Domain;

namespace Njord.Core.Actors;

public sealed record RequestForecastSink(long RequestId);

public abstract record RequestForecastSinkResponse(long RequestId);
public sealed record RequestForecastSinkCompleted(long RequestId, ISinkRef<DomainDataReceived> SinkRef)
    : RequestForecastSinkResponse(RequestId);
public sealed record RequestForecastSinkFailed(long RequestId, Exception Cause)
    : RequestForecastSinkResponse(RequestId);
