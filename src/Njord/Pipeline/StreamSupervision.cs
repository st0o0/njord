using System.Net.Http;
using Akka.Actor;
using StreamDecider = Akka.Streams.Supervision.Decider;
using StreamDirective = Akka.Streams.Supervision.Directive;

namespace Njord.Pipeline;

public static class StreamSupervision
{
    public static StreamDecider LoggingDecider(ILogger logger) => ex =>
    {
        var directive = Classify(ex);
        logger.LogWarning(ex, "Stream supervision: {Directive} for {ExceptionType}", directive, ex.GetType().Name);
        return directive;
    };

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
