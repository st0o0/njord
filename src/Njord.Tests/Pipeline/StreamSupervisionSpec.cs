using Akka.Actor;
using Microsoft.Extensions.Logging;
using Njord.Pipeline;
using StreamDirective = Akka.Streams.Supervision.Directive;

namespace Njord.Tests.Pipeline;

public sealed class StreamSupervisionSpec
{
    private readonly RecordingLogger _logger = new();

    [Fact(Timeout = 5000)]
    public void AskTimeoutException_resumes()
    {
        var decider = StreamSupervision.LoggingDecider(_logger);
        Assert.Equal(StreamDirective.Resume, decider(new AskTimeoutException("test")));
        Assert.Single(_logger.Entries);
    }

    [Fact(Timeout = 5000)]
    public void TaskCanceledException_resumes()
    {
        var decider = StreamSupervision.LoggingDecider(_logger);
        Assert.Equal(StreamDirective.Resume, decider(new TaskCanceledException()));
    }

    [Fact(Timeout = 5000)]
    public void OperationCanceledException_resumes()
    {
        var decider = StreamSupervision.LoggingDecider(_logger);
        Assert.Equal(StreamDirective.Resume, decider(new OperationCanceledException()));
    }

    [Fact(Timeout = 5000)]
    public void TimeoutException_resumes()
    {
        var decider = StreamSupervision.LoggingDecider(_logger);
        Assert.Equal(StreamDirective.Resume, decider(new TimeoutException()));
    }

    [Fact(Timeout = 5000)]
    public void HttpRequestException_resumes()
    {
        var decider = StreamSupervision.LoggingDecider(_logger);
        Assert.Equal(StreamDirective.Resume, decider(new HttpRequestException()));
    }

    [Fact(Timeout = 5000)]
    public void NullReferenceException_stops()
    {
        var decider = StreamSupervision.LoggingDecider(_logger);
        Assert.Equal(StreamDirective.Stop, decider(new NullReferenceException()));
        Assert.Single(_logger.Entries);
    }

    [Fact(Timeout = 5000)]
    public void InvalidOperationException_stops()
    {
        var decider = StreamSupervision.LoggingDecider(_logger);
        Assert.Equal(StreamDirective.Stop, decider(new InvalidOperationException("test")));
    }

    [Fact(Timeout = 5000)]
    public void Logger_is_called_for_every_exception()
    {
        var decider = StreamSupervision.LoggingDecider(_logger);
        decider(new AskTimeoutException("t1"));
        decider(new NullReferenceException());
        decider(new TimeoutException());

        Assert.Equal(3, _logger.Entries.Count);
        Assert.All(_logger.Entries, e => Assert.Equal(LogLevel.Warning, e.Level));
    }

    private sealed class RecordingLogger : ILogger
    {
        public List<(LogLevel Level, string Message)> Entries { get; } = [];

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            Entries.Add((logLevel, formatter(state, exception)));
        }
    }
}
