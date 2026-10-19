using Akka.Actor;
using Akka.Event;
using Microsoft.Extensions.Logging;
using StreamDecider = Akka.Streams.Supervision.Decider;
using StreamDirective = Akka.Streams.Supervision.Directive;

namespace Njord.Core.Actors;

public static class StreamSupervision
{
    public static StreamDecider LoggingDecider(ILoggingAdapter log) => ex =>
    {
        var cause = Unwrap(ex);
        if (IsTransient(cause))
            log.Warning(cause, "Stream supervision: Resume for transient {ExceptionType}", cause.GetType().Name);
        else
            log.Error(cause, "Stream supervision: Resume for unexpected {ExceptionType} — element dropped, state preserved", cause.GetType().Name);
        return StreamDirective.Resume;
    };

    public static StreamDecider LoggingDecider(ILogger logger) => ex =>
    {
        var cause = Unwrap(ex);
        if (IsTransient(cause))
            logger.LogWarning(cause, "Stream supervision: Resume for transient {ExceptionType}", cause.GetType().Name);
        else
            logger.LogError(cause, "Stream supervision: Resume for unexpected {ExceptionType} — element dropped, state preserved", cause.GetType().Name);
        return StreamDirective.Resume;
    };

    // SelectAsync/SelectAsyncUnordered hand an already-faulted task's exception over as an AggregateException.
    private static Exception Unwrap(Exception ex)
    {
        while (ex is AggregateException { InnerExceptions.Count: 1 } aggregate)
        {
            ex = aggregate.InnerExceptions[0];
        }

        return ex;
    }

    private static bool IsTransient(Exception ex) => ex is
        AskTimeoutException or
        TaskCanceledException or
        OperationCanceledException or
        TimeoutException or
        HttpRequestException;
}
