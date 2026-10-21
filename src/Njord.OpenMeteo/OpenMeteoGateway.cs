using System.Collections.Immutable;
using Akka.Actor;
using Akka.Event;
using Akka.Streams;
using Akka.Streams.Dsl;
using Microsoft.Extensions.Options;
using Njord.Core;
using Njord.Core.Actors;
using Njord.Core.Configuration;
using Njord.Core.Domain;
using Njord.Core.Polling;
using Njord.Forecast;
using Njord.Messages.Forecast;
using Njord.OpenMeteo.Mapping;
using Servus.Akka;

namespace Njord.OpenMeteo;

// Request/response gateway: every FetchDomain ends with exactly one Completed, Failed or Rejected reply.
// The pipeline hubs outlive this actor, so it attaches once in PreStart; a failed or lost attach crashes the
// actor and supervision restarts it.
public sealed class OpenMeteoGateway : ReceiveActor
{
    private const int ForecastBranchSize = 16;

    // Ids are unique across incarnations: a restarted gateway keeps its ActorRef, so late results of the
    // previous incarnation must never match anything of the current one.
    private static long LastId;

    private sealed record PipelineResolved(IActorRef Pipeline);
    private sealed record AttachFailed(string Reason, Exception? Cause);
    private sealed record BranchEnded(long BranchId, Exception? Cause) : IDeadLetterSuppression;
    private sealed record DomainFetchDone(long RequestId, DomainKind Domain, IDomainForecast Result, ImmutableDictionary<string, IDomainForecast> Models);
    private sealed record DomainFetchFailed(long RequestId, PollFailureReason Reason, string? Detail);
    private sealed record DomainBranchOffered(long RequestId, int Hash, IQueueOfferResult Result);

    private sealed record PendingDomainRequest(IActorRef Requester, Location Location, DomainKind Domain, Location Resolved);

    private readonly IReadOnlyDictionary<DomainKind, IDomainClient> _clients;
    private readonly TimeProvider _timeProvider;
    private readonly NjordOptions _options;
    private readonly FetchTimeouts _timeouts;
    private readonly IGatewayBudgetGate _gate;
    private readonly Dictionary<long, PendingDomainRequest> _pending = new();
    private readonly ILoggingAdapter _log = Context.GetLogger();

    private ISourceQueueWithComplete<DomainDataReceived>? _forecastQueue;
    private long _branchId;

    public OpenMeteoGateway(
        IReadOnlyDictionary<DomainKind, IDomainClient> clients,
        TimeProvider timeProvider,
        IOptions<NjordOptions> options,
        IBudgetProvider budgetProvider,
        FetchTimeouts timeouts)
    {
        _clients = clients;
        _timeProvider = timeProvider;
        _options = options.Value;
        _timeouts = timeouts;
        _gate = new GatewayBudgetGate(budgetProvider, Context.GetActor<IBudgetTracker>(), timeProvider);

        Receive<FetchDomain>(OnFetchDomain);
        Receive<DomainFetchDone>(OnDomainFetchDone);
        Receive<DomainFetchFailed>(msg => Reply(msg.RequestId, p => new FetchDomainFailed(p.Resolved, p.Domain, msg.Reason, msg.Detail)));
        Receive<DomainBranchOffered>(OnDomainBranchOffered);

        Receive<PipelineResolved>(OnPipelineResolved);
        Receive<RequestForecastSinkCompleted>(OnSinkCompleted);
        Receive<RequestForecastSinkFailed>(msg => throw new InvalidOperationException("The pipeline could not create a SinkRef", msg.Cause));
        Receive<AttachFailed>(msg => throw new InvalidOperationException($"Not attached to the pipeline input: {msg.Reason}", msg.Cause));
        Receive<BranchEnded>(msg => msg.BranchId == _branchId,
            msg => throw new InvalidOperationException("The forecast branch to the pipeline ended", msg.Cause));
        Receive<Terminated>(_ => throw new InvalidOperationException("The pipeline terminated"));
    }

    protected override void PreStart()
    {
        Context.GetActorAsync<IForecastPipeline>().PipeTo(Self,
            success: pipeline => new PipelineResolved(pipeline),
            failure: ex => new AttachFailed("the pipeline could not be resolved", ex));
    }

    protected override void PostStop() => _forecastQueue?.Complete();

    private void OnFetchDomain(FetchDomain msg)
    {
        var location = _options.FindLocation(msg.Location.Name);
        if (location is null)
        {
            Sender.Tell(new FetchDomainFailed(msg.Location, msg.Domain,
                PollFailureReason.ModelUnavailable, $"Location '{msg.Location}' not found in config"));
            return;
        }

        var id = NextId();
        _pending[id] = new PendingDomainRequest(Sender, msg.Location, msg.Domain, new Location(location.Name, location.Latitude.GetValueOrDefault(), location.Longitude.GetValueOrDefault()));

        if (!_clients.TryGetValue(msg.Domain, out var client))
        {
            Reply(id, p => new FetchDomainRejected(p.Resolved, p.Domain, FetchRejectionReason.PipelineUnavailable));
            return;
        }

        if (!_gate.TryAcquire(client.CallWeight))
        {
            Reply(id, p => new FetchDomainRejected(p.Resolved, p.Domain, FetchRejectionReason.Budget));
            return;
        }

        FetchAsync(id, client, location).PipeTo(Self,
            success: result => result,
            failure: ex =>
            {
                _log.Warning(ex, "Domain fetch for {Location}/{Domain} threw {Type}", msg.Location, msg.Domain, ex.GetType().Name);
                return new DomainFetchFailed(id, PollFailureReason.Transport, $"Fetch threw {ex.GetType().Name}: {ex.Message}");
            });
    }

    private async Task<object> FetchAsync(long id, IDomainClient client, LocationOptions location)
    {
        var domain = client.Domain;
        var domainTag = domain.ToTelemetryTag();
        var tags = new[]
        {
            new KeyValuePair<string, object?>("location", location.Name),
            new KeyValuePair<string, object?>("domain", domainTag),
        };

        var sw = System.Diagnostics.Stopwatch.StartNew();
        FetchResult result;
        try
        {
            result = await client.FetchAsync(location.Latitude.GetValueOrDefault(), location.Longitude.GetValueOrDefault(),
                _timeProvider.GetUtcNow(), CancellationToken.None);
        }
        catch
        {
            Telemetry.FetchTotal.Add(1, tags[0], tags[1], new KeyValuePair<string, object?>("outcome", "failure"));
            throw;
        }

        sw.Stop();
        var success = result is FetchSuccess;
        Telemetry.FetchTotal.Add(1, tags[0], tags[1], new KeyValuePair<string, object?>("outcome", success ? "success" : "failure"));
        if (success)
        {
            Telemetry.FetchDuration.Record(sw.Elapsed.TotalSeconds, tags[0], tags[1]);
        }

        return result switch
        {
            FetchSuccess ok => new DomainFetchDone(id, domain, ok.Forecast, ok.Models),
            FetchError error => new DomainFetchFailed(id, error.Reason, error.Message),
            _ => new DomainFetchFailed(id, PollFailureReason.MalformedPayload, $"The {domainTag} endpoint returned no usable data"),
        };
    }

    private void OnDomainFetchDone(DomainFetchDone msg)
    {
        if (!_pending.TryGetValue(msg.RequestId, out var pending))
        {
            return;
        }

        if (_forecastQueue is not { } branch)
        {
            Reply(msg.RequestId, p => new FetchDomainRejected(p.Resolved, p.Domain, FetchRejectionReason.PipelineUnavailable));
            return;
        }

        var hash = msg.Result.GetHashCode();
        // Per-model data first so the primary forecast is evaluated with all models merged in.
        var offers = msg.Models
            .Select(model => new DomainDataReceived(pending.Resolved, msg.Domain, model.Value, model.Key))
            .Append(new DomainDataReceived(pending.Resolved, msg.Domain, msg.Result))
            .ToList();

        OfferAllAsync(branch, offers).PipeTo(Self,
            success: result => new DomainBranchOffered(msg.RequestId, hash, result),
            failure: ex => new DomainBranchOffered(msg.RequestId, hash, new QueueOfferResult.Failure(ex)));
    }

    private static async Task<IQueueOfferResult> OfferAllAsync(
        ISourceQueueWithComplete<DomainDataReceived> branch,
        IReadOnlyList<DomainDataReceived> offers)
    {
        IQueueOfferResult last = QueueOfferResult.Enqueued.Instance;
        foreach (var offer in offers)
        {
            last = await branch.OfferAsync(offer);
            if (last is not QueueOfferResult.Enqueued)
            {
                return last;
            }
        }

        return last;
    }

    private void OnDomainBranchOffered(DomainBranchOffered msg) =>
        Reply(msg.RequestId, p => msg.Result is QueueOfferResult.Enqueued
            ? new FetchDomainCompleted(p.Resolved, p.Domain, msg.Hash)
            : new FetchDomainRejected(p.Resolved, p.Domain, FetchRejectionReason.PipelineUnavailable));

    private void OnPipelineResolved(PipelineResolved msg)
    {
        Context.Watch(msg.Pipeline);
        msg.Pipeline.Ask<RequestForecastSinkResponse>(new RequestForecastSink(NextId()), _timeouts.Admission).PipeTo(Self,
            success: response => response,
            failure: ex => new AttachFailed("the pipeline did not answer the SinkRef request", ex));
    }

    private void OnSinkCompleted(RequestForecastSinkCompleted msg)
    {
        var branchId = _branchId = NextId();
        var (queue, done) = Source.Queue<DomainDataReceived>(ForecastBranchSize, OverflowStrategy.DropHead)
            .WatchTermination(Keep.Both)
            .To(msg.SinkRef.Sink)
            .Run(Context.Materializer());
        done.PipeTo(Self,
            success: () => new BranchEnded(branchId, null),
            failure: ex => new BranchEnded(branchId, ex));
        _forecastQueue = queue;
        _log.Info("Attached to the pipeline input");
    }

    private void Reply(long requestId, Func<PendingDomainRequest, FetchDomainResponse> reply)
    {
        if (_pending.Remove(requestId, out var p))
        {
            p.Requester.Tell(reply(p));
        }
    }

    private static long NextId() => Interlocked.Increment(ref LastId);
}
