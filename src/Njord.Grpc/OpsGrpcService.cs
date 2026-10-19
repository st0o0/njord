using System.Diagnostics;
using System.Reflection;
using System.Runtime.ExceptionServices;
using Akka.Hosting;
using Google.Protobuf.WellKnownTypes;
using Grpc.Core;
using Microsoft.Extensions.Logging;
using Njord.Core;
using Njord.Grpc.Mapping;
using Njord.Grpc.V2;
using Njord.Messages.Budget;
using Njord.Messages.Config;
using Njord.Messages.Pipeline;
using Njord.Model;

namespace Njord.Grpc;

public sealed class OpsGrpcService(
    ActorRegistry actorRegistry,
    TimeProvider timeProvider,
    ServiceInfo serviceInfo,
    ILogger<OpsGrpcService> logger) : OpsService.OpsServiceBase
{
    private static readonly string Version =
        typeof(OpsGrpcService).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()
            ?.InformationalVersion ?? "unknown";

    private static readonly DateTimeOffset ProcessStart = Process.GetCurrentProcess().StartTime.ToUniversalTime();

    public override async Task<StatusResponse> GetStatus(GetStatusRequest request, ServerCallContext context)
    {
        var uptime = timeProvider.GetUtcNow() - ProcessStart;

        // Without an answer from the tracker the status reports zero usage against the configured budget.
        var budget = new BudgetStatusResult(0, 0, serviceInfo.MonthlyBudgetLimit,
            serviceInfo.DailyBudgetLimit);
        var budgetPartial = false;
        try
        {
            var response =
                await actorRegistry.AskAsync<IBudgetTracker, BudgetStatusResponse>(new QueryBudgetStatus(), context);
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

        var status = new StatusResponse
        {
            Version = Version,
            BudgetPartial = budgetPartial,
            UptimeSeconds = (long)uptime.TotalSeconds,
            ProcessStart = ProcessStart.ToTimestamp(),
            Budget = budget.ToProto(),
        };

        if (await AskPollStatesAsync(context) is { } entries)
        {
            status.Models.AddRange(entries.Select(e => e.ToModelStatus()));
        }
        else
        {
            logger.LogWarning("PollManager did not respond in time, returning status without model poll states");
            status.ModelsPartial = true;
        }

        status.ActiveEnrichments.AddRange(serviceInfo.ActiveEnrichments);
        return status;
    }

    public override async Task<GetTargetsResponse> GetTargets(GetTargetsRequest request, ServerCallContext context)
    {
        var response = new GetTargetsResponse();

        if (await AskPollStatesAsync(context) is { } entries)
        {
            response.Targets.AddRange(entries.Select(e => e.ToTriggerTarget()));
        }
        else
        {
            logger.LogWarning("PollManager did not respond in time, returning empty trigger targets");
        }

        return response;
    }

    public override async Task<TriggerPollResponse> TriggerPoll(TriggerPollRequest request, ServerCallContext context)
    {
        var location = string.IsNullOrEmpty(request.Location) ? (Location?)null : new Location(request.Location, 0, 0);
        var response = await actorRegistry.AskAsync<IPollManager, TriggerImmediatePollResponse>(
            new TriggerImmediatePoll(location), context);

        switch (response)
        {
            case TriggerImmediatePollCompleted completed:
                return completed.ToProto();
            case TriggerImmediatePollFailed failed:
                ExceptionDispatchInfo.Capture(failed.Cause).Throw();
                break;
        }

        throw new RpcException(new Status(StatusCode.Internal, $"Unexpected reply: {response.GetType().Name}"));
    }

    private async Task<IReadOnlyList<PollStateEntry>?> AskPollStatesAsync(ServerCallContext context)
    {
        try
        {
            var response =
                await actorRegistry.AskAsync<IPollManager, PollStatesResponse>(new QueryPollStates(), context);
            return response is PollStatesResult result ? result.Entries : null;
        }
        catch (RpcException ex) when (ex.StatusCode == StatusCode.DeadlineExceeded)
        {
            return null;
        }
    }
}
