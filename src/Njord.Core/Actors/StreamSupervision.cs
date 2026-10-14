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
        var directive = Classify(cause);
        log.Warning(cause, "Stream supervision: {Directive} for {ExceptionType}", directive, cause.GetType().Name);
        return directive;
    };

    public static StreamDecider LoggingDecider(ILogger logger) => ex =>
    {
        var cause = Unwrap(ex);
        var directive = Classify(cause);
        logger.LogWarning(cause, "Stream supervision: {Directive} for {ExceptionType}", directive, cause.GetType().Name);
        return directive;
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

    private static StreamDirective Classify(Exception ex) => ex switch
    {
        AskTimeoutException => StreamDirective.Resume,
        TaskCanceledException => StreamDirective.Resume,
        OperationCanceledException => StreamDirective.Resume,
        TimeoutException => StreamDirective.Resume,
        HttpRequestException => StreamDirective.Resume,
        _ => StreamDirective.Stop,
    };
}
