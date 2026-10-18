using Akka.Streams;
using Njord.Messages.Config;
using Njord.Messages.Pipeline;
using WeatherForecast = Njord.Forecast.Forecast;

namespace Njord.Core.Actors;

public sealed record RequestPipelineOutput(long RequestId);

public abstract record RequestPipelineOutputResponse(long RequestId);
public sealed record RequestPipelineOutputCompleted(long RequestId, ISourceRef<PipelineEvent> SourceRef)
    : RequestPipelineOutputResponse(RequestId);
public sealed record RequestPipelineOutputFailed(long RequestId, Exception Cause)
    : RequestPipelineOutputResponse(RequestId);

public sealed record RequestForecastSink(long RequestId);

public abstract record RequestForecastSinkResponse(long RequestId);
public sealed record RequestForecastSinkCompleted(long RequestId, ISinkRef<WeatherForecast> SinkRef)
    : RequestForecastSinkResponse(RequestId);
public sealed record RequestForecastSinkFailed(long RequestId, Exception Cause)
    : RequestForecastSinkResponse(RequestId);

public sealed record RequestReadModelStream(long RequestId);

public abstract record RequestReadModelStreamResponse(long RequestId);
public sealed record RequestReadModelStreamCompleted(long RequestId, ISourceRef<PipelineEvent> SourceRef)
    : RequestReadModelStreamResponse(RequestId);
public sealed record RequestReadModelStreamFailed(long RequestId, Exception Cause)
    : RequestReadModelStreamResponse(RequestId);

public sealed record RequestConfigStream(long RequestId);

public abstract record RequestConfigStreamResponse(long RequestId);
public sealed record RequestConfigStreamCompleted(long RequestId, ISourceRef<EffectiveConfig> SourceRef)
    : RequestConfigStreamResponse(RequestId);
public sealed record RequestConfigStreamFailed(long RequestId, Exception Cause)
    : RequestConfigStreamResponse(RequestId);
