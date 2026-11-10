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
using Njord.Grpc.Mapping;
using Njord.Messages.Budget;
using Njord.Messages.Polling;
using Njord.Messages.Results;
using ActorSystem = Akka.Actor.ActorSystem;
using GrpcStatus = Grpc.Core.Status;

namespace Njord.Grpc;

public sealed class NjordGrpcService(
    ActorRegistry actorRegistry,
    ActorSystem actorSystem,
    EntityRegistry registry,
    TimeProvider timeProvider,
    IOptions<PollingOptions> polling,
    IEnumerable<IFeatureRule> features,
    ILogger<NjordGrpcService> logger) : Proto.NjordService.NjordServiceBase
{
    private static readonly string _version =
        typeof(NjordGrpcService).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()
            ?.InformationalVersion ?? "unknown";

    private static readonly DateTimeOffset _processStart = Process.GetCurrentProcess().StartTime.ToUniversalTime();

    private readonly IMaterializer _mat = actorSystem.Materializer();

    public override Task<Proto.ManifestResponse> GetManifest(Proto.ManifestRequest request, ServerCallContext context)
    {
        var response = new Proto.ManifestResponse();
        response.Entities.AddRange(registry.All.Select(e => e.ToEntityDefinition()));
        return Task.FromResult(response);
    }

    public override async Task Subscribe(
        Proto.SubscribeRequest request,
        IServerStreamWriter<Proto.EntityEvent> responseStream,
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
            : registry.All
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
            .Select(static e => e.ToProto())
            .Select(e => wanted is null ? e : Filter(e, wanted))
            .Where(static e => e is not null)
            .Via(killSwitch.Flow<Proto.EntityEvent?>())
            .SelectAsync(1, async e =>
            {
                await responseStream.WriteAsync(e!, context.CancellationToken).ConfigureAwait(false);
                return e;
            })
            .RunWith(Sink.Ignore<Proto.EntityEvent?>(), _mat).ConfigureAwait(false);
    }

    public override async Task<Proto.StatusResponse> GetStatus(Proto.StatusRequest request, ServerCallContext context)
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

        var status = new Proto.StatusResponse
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

    public override async Task<Proto.TriggerPollResponse> TriggerPoll(Proto.TriggerPollRequest request, ServerCallContext context)
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

    private static Proto.EntityEvent? Filter(Proto.EntityEvent evt, HashSet<string> wanted)
    {
        switch (evt.EventCase)
        {
            case Proto.EntityEvent.EventOneofCase.Snapshot:
                var snapshot = new Proto.EntitySnapshot();
                snapshot.States.AddRange(evt.Snapshot.States.Where(s => wanted.Contains(s.EntityId)));
                return new Proto.EntityEvent { Snapshot = snapshot };
            case Proto.EntityEvent.EventOneofCase.StateUpdate:
                return wanted.Contains(evt.StateUpdate.EntityId) ? evt : null;
            case Proto.EntityEvent.EventOneofCase.Unavailable:
                return wanted.Contains(evt.Unavailable.EntityId) ? evt : null;
            case Proto.EntityEvent.EventOneofCase.Available:
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
