using Akka.Streams;
using Njord.Messages.Pipeline;
using WeatherForecast = Njord.Weather.Forecast;

namespace Njord.Core.Actors;

public sealed record RequestPipelineOutput(long RequestId);

public abstract record RequestPipelineOutputResponse(long RequestId);
public sealed record RequestPipelineOutputCompleted(long RequestId, ISourceRef<PipelineEvent> SourceRef)
    : RequestPipelineOutputResponse(RequestId);
public sealed record RequestPipelineOutputFailed(long RequestId, Exception Cause)
    : RequestPipelineOutputResponse(RequestId);

public sealed record RequestForecastStream(Guid RequestId);

public abstract record RequestForecastStreamResponse(Guid RequestId);
public sealed record RequestForecastStreamCompleted(Guid RequestId, ISourceRef<WeatherForecast> SourceRef)
    : RequestForecastStreamResponse(RequestId);
public sealed record RequestForecastStreamFailed(Guid RequestId, Exception Cause)
    : RequestForecastStreamResponse(RequestId);
