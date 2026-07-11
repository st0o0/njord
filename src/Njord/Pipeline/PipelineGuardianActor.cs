using Akka.Actor;
using Akka.Streams;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Njord.Configuration;
using Njord.Ingest;

namespace Njord.Pipeline;

/// <summary>
/// Owns the poll pipeline's lifecycle: materializes the stream with a
/// materializer bound to this actor's context (so it dies with the actor) and
/// writes exactly one summary log per cycle. Later changes hang consensus and
/// MQTT egress off this coordination point.
/// </summary>
public sealed class PipelineGuardianActor : ReceiveActor
{
    private readonly NjordOptions _options;
    private readonly IOpenMeteoClient _client;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<PipelineGuardianActor> _logger;

    public PipelineGuardianActor(
        IOptions<NjordOptions> options,
        IOpenMeteoClient client,
        TimeProvider timeProvider,
        ILogger<PipelineGuardianActor> logger)
    {
        _options = options.Value;
        _client = client;
        _timeProvider = timeProvider;
        _logger = logger;

        Receive<CycleResult>(result => _logger.LogInformation(
            "Poll cycle {Cycle}: {Received} received, {Failed} failed, {Unanswered} unanswered — {Summary}",
            result.Cycle,
            result.Received.Count,
            result.Failed.Count,
            result.Unanswered.Count,
            FormatSummary(result)));
    }

    protected override void PreStart()
    {
        var materializer = Context.Materializer();
        var self = Self;
        PollPipeline
            .Create(_options, _client, _timeProvider, materializer)
            .RunForeach(result => self.Tell(result), materializer);
    }

    internal static string FormatSummary(CycleResult result)
    {
        var parts = result.Received.Select(f => $"{f.Location}/{f.Model.Id} ok")
            .Concat(result.Failed.Select(f => $"{f.Location}/{f.Model.Id} failed ({f.Reason})"))
            .Concat(result.Unanswered.Select(t => $"{t.Location}/{t.Model.Id} unanswered"));
        return string.Join(", ", parts);
    }
}
