using Akka.Streams;
using WeatherForecast = Njord.Weather.Forecast;

namespace Njord.Messages.Forecast;

public sealed record RequestForecastStream(Guid RequestId);

public abstract record RequestForecastStreamResponse(Guid RequestId);

public sealed record ForecastStreamReady(Guid RequestId, ISourceRef<WeatherForecast> SourceRef)
    : RequestForecastStreamResponse(RequestId);

public sealed record ForecastStreamFailed(Guid RequestId, Exception Cause)
    : RequestForecastStreamResponse(RequestId);
