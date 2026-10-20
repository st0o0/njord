using Akka.Actor;
using Akka.Event;
using Akka.Streams;
using Akka.Streams.Dsl;
using Microsoft.Extensions.Options;
using Njord.Core;
using Njord.Core.Actors;
using Njord.Core.Configuration;
using Njord.Core.Configuration.Polling;
using Njord.Core.Domain;
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
    private sealed record DomainFetchDone(long RequestId, DomainKind Domain, IDomainForecast Result);
    private sealed record DomainFetchFailed(long RequestId, PollFailureReason Reason, string? Detail);
    private sealed record DomainBranchOffered(long RequestId, int Hash, IQueueOfferResult Result);

    private sealed record PendingDomainRequest(IActorRef Requester, Location Location, DomainKind Domain, Location Resolved);

    private readonly IWeatherClient _weatherClient;
    private readonly IAirQualityClient _airQualityClient;
    private readonly IFloodClient _floodClient;
    private readonly IMarineClient _marineClient;
    private readonly TimeProvider _timeProvider;
    private readonly NjordOptions _options;
    private readonly WeatherOptions _weather;
    private readonly FetchTimeouts _timeouts;
    private readonly int _weatherWeight;
    private readonly IGatewayBudgetGate _gate;
    private readonly Dictionary<long, PendingDomainRequest> _pending = new();
    private readonly ILoggingAdapter _log = Context.GetLogger();

    private ISourceQueueWithComplete<DomainDataReceived>? _forecastQueue;
    private long _branchId;

    public OpenMeteoGateway(
        IWeatherClient weatherClient,
        IAirQualityClient airQualityClient,
        IFloodClient floodClient,
        IMarineClient marineClient,
        TimeProvider timeProvider,
        IOptions<NjordOptions> options,
        IOptions<WeatherOptions> weather,
        IBudgetProvider budgetProvider,
        FetchTimeouts timeouts)
    {
        _weatherClient = weatherClient;
        _airQualityClient = airQualityClient;
        _floodClient = floodClient;
        _marineClient = marineClient;
        _timeProvider = timeProvider;
        _options = options.Value;
        _weather = weather.Value;
        _timeouts = timeouts;
        _weatherWeight = ComputeWeight(weatherClient.HourlyVariables.Count, _weather.ForecastDays);
        _gate = new GatewayBudgetGate(budgetProvider, Context.GetActor<IBudgetTracker>(), timeProvider);

        Receive<FetchDomain>(OnFetchDomain);
        Receive<DomainFetchDone>(OnDomainFetchDone);
        Receive<DomainFetchFailed>(msg => Reply(msg.RequestId, p => new FetchDomainFailed(p.Location, p.Domain, msg.Reason, msg.Detail)));
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
        _pending[id] = new PendingDomainRequest(Sender, msg.Location, msg.Domain, new Location(location.Name, location.Latitude.Value, location.Longitude.Value));

        if (msg.Domain is not (DomainKind.Weather or DomainKind.AirQuality or DomainKind.Flood or DomainKind.Marine))
        {
            Reply(id, p => new FetchDomainRejected(p.Location, p.Domain, FetchRejectionReason.PipelineUnavailable));
            return;
        }

        if (!_gate.TryAcquire(msg.Domain == DomainKind.Weather ? _weatherWeight : 1))
        {
            Reply(id, p => new FetchDomainRejected(p.Location, p.Domain, FetchRejectionReason.Budget));
            return;
        }

        FetchAsync(id, msg.Domain, location).PipeTo(Self,
            success: result => result,
            failure: ex =>
            {
                _log.Warning(ex, "Domain fetch for {Location}/{Domain} threw {Type}", msg.Location, msg.Domain, ex.GetType().Name);
                return new DomainFetchFailed(id, PollFailureReason.Transport, $"Fetch threw {ex.GetType().Name}: {ex.Message}");
            });
    }

    private async Task<object> FetchAsync(long id, DomainKind domain, LocationOptions location)
    {
        var now = _timeProvider.GetUtcNow();
        var domainTag = domain.ToTelemetryTag();
        var locationTag = new KeyValuePair<string, object?>("location", location.Name);
        var domainTagPair = new KeyValuePair<string, object?>("domain", domainTag);

        var sw = System.Diagnostics.Stopwatch.StartNew();
        IDomainForecast? forecast;
        try
        {
            forecast = await FetchForecastAsync(domain, location, now);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or System.Text.Json.JsonException)
        {
            sw.Stop();
            Telemetry.FetchTotal.Add(1, locationTag, domainTagPair, new KeyValuePair<string, object?>("outcome", "failure"));
            return new DomainFetchFailed(id, PollFailureReason.Transport, $"Fetch failed with {ex.GetType().Name}: {ex.Message}");
        }

        sw.Stop();

        Telemetry.FetchTotal.Add(1, locationTag, domainTagPair,
            new KeyValuePair<string, object?>("outcome", forecast is not null ? "success" : "failure"));
        if (forecast is not null)
        {
            Telemetry.FetchDuration.Record(sw.Elapsed.TotalSeconds, locationTag, domainTagPair);
        }

        return forecast is null
            ? new DomainFetchFailed(id, PollFailureReason.MalformedPayload, $"The {domainTag} endpoint returned no usable data")
            : new DomainFetchDone(id, domain, forecast);
    }

    private async Task<IDomainForecast?> FetchForecastAsync(DomainKind domain, LocationOptions location, DateTimeOffset now) =>
        domain switch
        {
            DomainKind.Weather => (await _weatherClient.FetchAsync(location.Latitude.Value, location.Longitude.Value, _weather.ForecastDays, CancellationToken.None))
                ?.ToDomainForecast(now),
            DomainKind.AirQuality => (await _airQualityClient.FetchAsync(location.Latitude.Value, location.Longitude.Value, _weather.ForecastDays, CancellationToken.None))
                ?.ToDomainForecast(now),
            DomainKind.Marine => (await _marineClient.FetchAsync(location.Latitude.Value, location.Longitude.Value, CancellationToken.None))
                ?.ToDomainForecast(now),
            _ => (await _floodClient.FetchAsync(location.Latitude.Value, location.Longitude.Value, CancellationToken.None))
                ?.ToDomainForecast(now),
        };

    private void OnDomainFetchDone(DomainFetchDone msg)
    {
        if (!_pending.TryGetValue(msg.RequestId, out var pending))
        {
            return;
        }

        if (_forecastQueue is not { } branch)
        {
            Reply(msg.RequestId, p => new FetchDomainRejected(p.Location, p.Domain, FetchRejectionReason.PipelineUnavailable));
            return;
        }

        var data = new DomainDataReceived(pending.Resolved, msg.Domain, msg.Result);
        var hash = msg.Result.GetHashCode();

        branch.OfferAsync(data).PipeTo(Self,
            success: result => new DomainBranchOffered(msg.RequestId, hash, result),
            failure: ex => new DomainBranchOffered(msg.RequestId, hash, new QueueOfferResult.Failure(ex)));
    }

    private void OnDomainBranchOffered(DomainBranchOffered msg) =>
        Reply(msg.RequestId, p => msg.Result is QueueOfferResult.Enqueued
            ? new FetchDomainCompleted(p.Location, p.Domain, msg.Hash)
            : new FetchDomainRejected(p.Location, p.Domain, FetchRejectionReason.PipelineUnavailable));

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

    internal static int ComputeWeight(int hourlyVariableCount, int forecastDays) =>
        (int)Math.Ceiling(hourlyVariableCount / 10.0) * (int)Math.Ceiling(forecastDays / 14.0);
}
