using Akka.Actor;
using Akka.Streams;
using Akka.Streams.Dsl;
using Microsoft.Extensions.Logging;
using Njord.Core.Actors;
using Njord.Tests.Shared;
using StreamDirective = Akka.Streams.Supervision.Directive;

namespace Njord.Core.Tests;

public sealed class StreamSupervisionSpec
{
    private readonly RecordingLogger _logger = new();

    [Fact]
    public void AskTimeoutException_resumes_and_logs_warning()
    {
        var decider = StreamSupervision.LoggingDecider(_logger);
        Assert.Equal(StreamDirective.Resume, decider(new AskTimeoutException("test")));
        var entry = Assert.Single(_logger.Entries);
        Assert.Equal(LogLevel.Warning, entry.Level);
    }

    [Fact]
    public void TaskCanceledException_resumes()
    {
        var decider = StreamSupervision.LoggingDecider(_logger);
        Assert.Equal(StreamDirective.Resume, decider(new TaskCanceledException()));
    }

    [Fact]
    public void OperationCanceledException_resumes()
    {
        var decider = StreamSupervision.LoggingDecider(_logger);
        Assert.Equal(StreamDirective.Resume, decider(new OperationCanceledException()));
    }

    [Fact]
    public void TimeoutException_resumes()
    {
        var decider = StreamSupervision.LoggingDecider(_logger);
        Assert.Equal(StreamDirective.Resume, decider(new TimeoutException()));
    }

    [Fact]
    public void HttpRequestException_resumes()
    {
        var decider = StreamSupervision.LoggingDecider(_logger);
        Assert.Equal(StreamDirective.Resume, decider(new HttpRequestException()));
    }

    [Fact]
    public void NullReferenceException_resumes_and_logs_error()
    {
        var decider = StreamSupervision.LoggingDecider(_logger);
        Assert.Equal(StreamDirective.Resume, decider(new NullReferenceException()));
        var entry = Assert.Single(_logger.Entries);
        Assert.Equal(LogLevel.Error, entry.Level);
    }

    [Fact]
    public void InvalidOperationException_resumes_and_logs_error()
    {
        var decider = StreamSupervision.LoggingDecider(_logger);
        Assert.Equal(StreamDirective.Resume, decider(new InvalidOperationException("test")));
        var entry = Assert.Single(_logger.Entries);
        Assert.Equal(LogLevel.Error, entry.Level);
    }

    [Fact]
    public void Transient_exceptions_log_as_warning_and_unexpected_as_error()
    {
        var decider = StreamSupervision.LoggingDecider(_logger);
        decider(new AskTimeoutException("t1"));
        decider(new NullReferenceException());
        decider(new TimeoutException());

        Assert.Equal(3, _logger.Entries.Count);
        Assert.Equal(LogLevel.Warning, _logger.Entries[0].Level);
        Assert.Equal(LogLevel.Error, _logger.Entries[1].Level);
        Assert.Equal(LogLevel.Warning, _logger.Entries[2].Level);
    }

    [Fact]
    public void Wrapped_transient_exception_resumes_and_logs_the_inner_type()
    {
        var decider = StreamSupervision.LoggingDecider(_logger);

        var directive = decider(new AggregateException(new TimeoutException("slow")));

        Assert.Equal(StreamDirective.Resume, directive);
        var entry = Assert.Single(_logger.Entries);
        Assert.Contains(nameof(TimeoutException), entry.Message);
    }

    [Fact]
    public void Doubly_wrapped_transient_exception_resumes()
    {
        var decider = StreamSupervision.LoggingDecider(_logger);

        var directive = decider(new AggregateException(new AggregateException(new HttpRequestException("reset"))));

        Assert.Equal(StreamDirective.Resume, directive);
    }

    [Fact]
    public void Wrapped_unexpected_exception_resumes_and_logs_error()
    {
        var decider = StreamSupervision.LoggingDecider(_logger);

        var directive = decider(new AggregateException(new InvalidOperationException("bad json")));

        Assert.Equal(StreamDirective.Resume, directive);
        var entry = Assert.Single(_logger.Entries);
        Assert.Equal(LogLevel.Error, entry.Level);
    }

    [Fact]
    public void Aggregate_with_several_causes_resumes_and_logs_error()
    {
        var decider = StreamSupervision.LoggingDecider(_logger);

        var directive = decider(new AggregateException(new TimeoutException(), new HttpRequestException()));

        Assert.Equal(StreamDirective.Resume, directive);
        var entry = Assert.Single(_logger.Entries);
        Assert.Equal(LogLevel.Error, entry.Level);
    }

    [Fact(Timeout = 10_000)]
    public async Task Already_faulted_task_in_SelectAsyncUnordered_resumes_the_stream()
    {
        using var system = ActorSystem.Create($"supervision-{Guid.NewGuid():N}");

        var results = await Source.From([1, 2, 3])
            .SelectAsyncUnordered(2, i => i == 2
                ? Task.FromException<int>(new TimeoutException("faulted before the stage saw it"))
                : Task.FromResult(i))
            .WithAttributes(ActorAttributes.CreateSupervisionStrategy(StreamSupervision.LoggingDecider(_logger)))
            .RunWith(Sink.Seq<int>(), system.Materializer())
            .WaitAsync(TestTimeouts.RealTimeWaitMax, TestContext.Current.CancellationToken);

        Assert.Equal([1, 3], results.Order());
        var entry = Assert.Single(_logger.Entries);
        Assert.Contains(nameof(TimeoutException), entry.Message);
    }
}
