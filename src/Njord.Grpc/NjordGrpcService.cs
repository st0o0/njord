using System.Diagnostics;
using System.Reflection;
using System.Runtime.ExceptionServices;
using Akka.Hosting;
using Akka.Streams;
using Akka.Streams.Dsl;
using Google.Protobuf.WellKnownTypes;
using Grpc.Core;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Njord.Core;
using Njord.Core.Configuration;
using Njord.Grpc.Mapping.V3;
using Njord.Messages.Budget;
using Njord.Messages.Polling;
using Njord.Messages.Results;
using ActorSystem = Akka.Actor.ActorSystem;
using GrpcStatus = Grpc.Core.Status;

namespace Njord.Grpc;

public sealed class NjordGrpcService(
    ActorRegistry actorRegistry,
    ActorSystem actorSystem,
    ManifestBuilder manifest,
    TimeProvider timeProvider,
    IOptions<PollingOptions> polling,
    IEnumerable<IFeatureRule> features,
    ILogger<NjordGrpcService> logger) : V3.NjordService.NjordServiceBase
{
    private static readonly string _version =
        typeof(NjordGrpcService).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()
            ?.InformationalVersion ?? "unknown";

    private static readonly DateTimeOffset _processStart = Process.GetCurrentProcess().StartTime.ToUniversalTime();

    private readonly IMaterializer _mat = actorSystem.Materializer();

    public override Task<V3.ManifestResponse> GetManifest(V3.ManifestRequest request, ServerCallContext context)
    {
        var response = new V3.ManifestResponse();
        response.Entities.AddRange(manifest.Entities);
        return Task.FromResult(response);
    }

    public override async Task Subscribe(
        V3.SubscribeRequest request,
        IServerStreamWriter<V3.EntityEvent> responseStream,
        ServerCallContext context)
    {
        var reply = await actorRegistry.AskAsync<IGrpcConsumer, RequestEntityStreamResponse>(
            new RequestEntityStream(0), context).ConfigureAwait(false);
        var sourceRef = reply switch
        {
            RequestEntityStreamCompleted completed => completed.SourceRef,
            _ => throw new RpcException(new GrpcStatus(StatusCode.Unavailable, "Entity stream is currently unavailable")),
        };

        var wanted = string.IsNullOrEmpty(request.Location)
            ? null
            : manifest.Entities
                .Where(e => string.Equals(e.Location, request.Location, StringComparison.OrdinalIgnoreCase))
                .Select(e => e.EntityId)
                .ToHashSet(StringComparer.Ordinal);
        if (wanted is { Count: 0 })
        {
            throw new RpcException(new GrpcStatus(StatusCode.NotFound, $"Location '{request.Location}' not found"));
        }

        var killSwitch = KillSwitches.Shared($"grpc-entities-{context.Peer}");
        await using var ctr = context.CancellationToken.Register(killSwitch.Shutdown).ConfigureAwait(false);

        await sourceRef.Source
            .Select(e => wanted is null ? e : Filter(e, wanted))
            .Where(e => e is not null)
            .Via(killSwitch.Flow<V3.EntityEvent?>())
            .SelectAsync(1, async e =>
            {
                await responseStream.WriteAsync(e!, context.CancellationToken).ConfigureAwait(false);
                return e;
            })
            .RunWith(Sink.Ignore<V3.EntityEvent?>(), _mat).ConfigureAwait(false);
    }

    public override async Task<V3.StatusResponse> GetStatus(V3.StatusRequest request, ServerCallContext context)
    {
        var uptime = timeProvider.GetUtcNow() - _processStart;

        var budget = new BudgetStatusResult(0, 0, polling.Value.Budget.RequestsPerMonth,
            polling.Value.Budget.DailyLimit);
        var budgetPartial = false;
        try
        {
            var response =
                await actorRegistry.AskAsync<IBudgetTracker, BudgetStatusResult>(new QueryBudgetStatus(), context).ConfigureAwait(false);
            if (response is BudgetStatusResult result)
            {
                budget = result;
            }
            else
            {
                budgetPartial = true;
            }
        }
        catch (RpcException ex) when (ex.StatusCode == StatusCode.DeadlineExceeded)
        {
            logger.LogWarning("BudgetTracker did not respond in time, returning status with zero usage");
            budgetPartial = true;
        }

        var status = new V3.StatusResponse
        {
            Version = _version,
            BudgetPartial = budgetPartial,
            UptimeSeconds = (long)uptime.TotalSeconds,
            ProcessStart = _processStart.ToTimestamp(),
            Budget = budget.ToProto(),
        };

        if (await AskPollStatesAsync(context).ConfigureAwait(false) is { } entries)
        {
            status.Models.AddRange(entries.Select(e => e.ToModelStatus()));
        }
        else
        {
            logger.LogWarning("PollManager did not respond in time, returning status without model poll states");
            status.ModelsPartial = true;
        }

        status.ActiveEnrichments.AddRange(features
            .Where(static rule => rule.Enabled)
            .Select(static rule => $"{rule.Descriptor.Category}:{rule.Descriptor.Name}")
            .Order(StringComparer.Ordinal));
        return status;
    }

    public override async Task<V3.TriggerPollResponse> TriggerPoll(V3.TriggerPollRequest request, ServerCallContext context)
    {
        var location = string.IsNullOrEmpty(request.Location) ? (Location?)null : new Location(request.Location, 0, 0);
        var response = await actorRegistry.AskAsync<IPollManager, TriggerImmediatePollResponse>(
            new TriggerImmediatePoll(location), context).ConfigureAwait(false);

        switch (response)
        {
            case TriggerImmediatePollCompleted completed:
                return completed.ToProto();
            case TriggerImmediatePollFailed failed:
                ExceptionDispatchInfo.Capture(failed.Cause).Throw();
                break;
        }

        throw new RpcException(new GrpcStatus(StatusCode.Internal, $"Unexpected reply: {response.GetType().Name}"));
    }

    private static V3.EntityEvent? Filter(V3.EntityEvent evt, HashSet<string> wanted)
    {
        switch (evt.EventCase)
        {
            case V3.EntityEvent.EventOneofCase.Snapshot:
                var snapshot = new V3.EntitySnapshot();
                snapshot.States.AddRange(evt.Snapshot.States.Where(s => wanted.Contains(s.EntityId)));
                return new V3.EntityEvent { Snapshot = snapshot };
            case V3.EntityEvent.EventOneofCase.StateUpdate:
                return wanted.Contains(evt.StateUpdate.EntityId) ? evt : null;
            case V3.EntityEvent.EventOneofCase.Unavailable:
                return wanted.Contains(evt.Unavailable.EntityId) ? evt : null;
            case V3.EntityEvent.EventOneofCase.Available:
                return wanted.Contains(evt.Available.EntityId) ? evt : null;
            default:
                return null;
        }
    }

    private async Task<IReadOnlyList<PollStateEntry>?> AskPollStatesAsync(ServerCallContext context)
    {
        try
        {
            var response =
                await actorRegistry.AskAsync<IPollManager, PollStatesResult>(new QueryPollStates(), context).ConfigureAwait(false);
            return response is PollStatesResult result ? result.Entries : null;
        }
        catch (RpcException ex) when (ex.StatusCode == StatusCode.DeadlineExceeded)
        {
            return null;
        }
    }
}
