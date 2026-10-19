using Akka.Streams;
using Akka.Streams.Dsl;
using Njord.Messages.Alerts;
using Njord.Messages.Forecast;
using Njord.Messages.Pipeline;
using Njord.Model;
using Njord.Tests.Shared;

namespace Njord.Grpc.Tests;

public sealed class EventCollector
{
    private readonly Lock _gate = new();
    private readonly List<PipelineEvent> _events = [];
    private readonly TaskCompletionSource _warmUpSeen = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private Func<IReadOnlyList<PipelineEvent>, bool>? _condition;
    private TaskCompletionSource? _conditionMet;

    public Sink<PipelineEvent, Task<Akka.Done>> AsSink() => Sink.ForEach<PipelineEvent>(Add);

    public Task WarmUpSeen => _warmUpSeen.Task;

    public async Task<IReadOnlyList<PipelineEvent>> WaitUntilAsync(
        Func<IReadOnlyList<PipelineEvent>, bool> condition, string description, CancellationToken ct)
    {
        var met = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        lock (_gate)
        {
            _condition = condition;
            _conditionMet = met;
            if (condition(_events))
            {
                met.TrySetResult();
            }
        }

        try
        {
            await met.Task.WaitAsync(TestTimeouts.RealTimeWaitMax, ct);
        }
        catch (TimeoutException)
        {
            Assert.Fail($"Timed out after {TestTimeouts.RealTimeWaitMax} waiting for {description}; " +
                        $"received {ReceivedEvents().Count} events");
        }

        return ReceivedEvents();
    }

    private void Add(PipelineEvent evt)
    {
        if (Equals(evt, GrpcConsumerSpecBase.WarmUp))
        {
            _warmUpSeen.TrySetResult();
            return;
        }

        lock (_gate)
        {
            _events.Add(evt);
            if (_condition is not null && _condition(_events))
            {
                _conditionMet?.TrySetResult();
            }
        }
    }

    private List<PipelineEvent> ReceivedEvents()
    {
        lock (_gate)
        {
            return [.. _events];
        }
    }
}
