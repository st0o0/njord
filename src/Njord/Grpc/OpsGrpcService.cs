using System.Diagnostics;
using System.Reflection;
using Akka.Actor;
using Akka.Hosting;
using Google.Protobuf.WellKnownTypes;
using Grpc.Core;
using Microsoft.Extensions.Options;
using Njord.Configuration;
using Njord.Grpc.V2;
using Njord.Pipeline;

namespace Njord.Grpc;

public sealed class OpsGrpcService(
    IOptionsMonitor<NjordOptions> optionsMonitor,
    ActorRegistry actorRegistry,
    TimeProvider timeProvider,
    ILogger<OpsGrpcService> logger) : V2.OpsService.OpsServiceBase
{
    private static readonly string Version =
        typeof(OpsGrpcService).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()
            ?.InformationalVersion ?? "unknown";

    private static readonly DateTimeOffset ProcessStart = Process.GetCurrentProcess().StartTime.ToUniversalTime();

    private static readonly TimeSpan AskTimeout = TimeSpan.FromSeconds(5);

    private readonly IOptionsMonitor<NjordOptions> _optionsMonitor = optionsMonitor;
    private readonly ActorRegistry _actorRegistry = actorRegistry;
    private readonly TimeProvider _timeProvider = timeProvider;
    private readonly ILogger<OpsGrpcService> _logger = logger;

    // ═══════════════════════════════════════
    // Status
    // ═══════════════════════════════════════

    public override async Task<StatusResponse> GetStatus(GetStatusRequest request, ServerCallContext context)
    {
        var uptime = _timeProvider.GetUtcNow() - ProcessStart;
        var budget = BudgetCalculator.GetEffectiveBudget(_optionsMonitor.CurrentValue);

        long monthlyUsed = 0;
        long dailyUsed = 0;
        try
        {
            var tracker = _actorRegistry.Get<BudgetTrackerActor>();
            var usage = await tracker.Ask<BudgetTrackerActor.BudgetUsage>(
                new BudgetTrackerActor.GetBudgetUsage(), AskTimeout);
            monthlyUsed = usage.MonthlyUsed;
            dailyUsed = usage.DailyUsed;
        }
        catch (AskTimeoutException)
        {
            _logger.LogWarning("BudgetTrackerActor did not respond within {Timeout}s — returning status with zero usage", AskTimeout.TotalSeconds);
        }

        var status = new StatusResponse
        {
            Version = Version,
            UptimeSeconds = (long)uptime.TotalSeconds,
            ProcessStart = Timestamp.FromDateTimeOffset(ProcessStart),
            Budget = new BudgetStatus
            {
                MonthlyLimit = budget.RequestsPerMonth,
                MonthlyUsed = monthlyUsed,
                DailyLimit = RequestBudget.OpenMeteoFreeTierDailyLimit,
                DailyUsed = dailyUsed,
                UsagePercent = budget.RequestsPerMonth > 0
                    ? (double)monthlyUsed / budget.RequestsPerMonth * 100
                    : 0,
            },
        };

        try
        {
            var scheduler = _actorRegistry.Get<SchedulerActor>();
            var snapshot = await scheduler.Ask<PollStatesSnapshot>(new GetPollStates(), AskTimeout);
            foreach (var entry in snapshot.Entries)
            {
                var modelStatus = new ModelStatus
                {
                    Location = entry.Location,
                    Model = entry.ModelId,
                    Phase = entry.Phase == PollPhase.Steady ? "steady" : "discovery",
                    NextPoll = Timestamp.FromDateTimeOffset(entry.NextPollUtc),
                    MissCount = entry.MissCount,
                };
                if (entry.LastChangeUtc.HasValue)
                {
                    modelStatus.LastChange = Timestamp.FromDateTimeOffset(entry.LastChangeUtc.Value);
                }

                if (entry.CycleSeconds.HasValue)
                {
                    modelStatus.CycleSeconds = entry.CycleSeconds.Value;
                }

                status.Models.Add(modelStatus);
            }
        }
        catch (AskTimeoutException)
        {
            _logger.LogWarning("SchedulerActor did not respond within {Timeout}s — returning status without model poll states", AskTimeout.TotalSeconds);
        }

        var enrichment = _optionsMonitor.CurrentValue.Enrichment;
        if (enrichment.Consensus.Enabled)
        {
            status.ActiveEnrichments.Add("consensus");
        }

        if (enrichment.Alerts.Enabled)
        {
            status.ActiveEnrichments.Add("alerts");
        }

        if (enrichment.Derived.Enabled)
        {
            status.ActiveEnrichments.Add("derived");
        }

        if (enrichment.Trends.Enabled)
        {
            status.ActiveEnrichments.Add("trends");
        }

        if (enrichment.Indices.Enabled)
        {
            status.ActiveEnrichments.Add("indices");
        }

        if (enrichment.History.Enabled)
        {
            status.ActiveEnrichments.Add("history");
        }

        return status;
    }

    // ═══════════════════════════════════════
    // Targets
    // ═══════════════════════════════════════

    public override async Task<GetTargetsResponse> GetTargets(GetTargetsRequest request, ServerCallContext context)
    {
        var response = new GetTargetsResponse();

        try
        {
            var scheduler = _actorRegistry.Get<SchedulerActor>();
            var snapshot = await scheduler.Ask<PollStatesSnapshot>(new GetPollStates(), AskTimeout);
            foreach (var entry in snapshot.Entries)
            {
                var target = new TriggerTarget
                {
                    Location = entry.Location,
                    Model = entry.ModelId,
                    Phase = entry.Phase == PollPhase.Steady ? "steady" : "discovery",
                    NextPoll = Timestamp.FromDateTimeOffset(entry.NextPollUtc),
                    MissCount = entry.MissCount,
                };
                if (entry.LastChangeUtc.HasValue)
                {
                    target.LastChange = Timestamp.FromDateTimeOffset(entry.LastChangeUtc.Value);
                }

                if (entry.CycleSeconds.HasValue)
                {
                    target.CycleSeconds = entry.CycleSeconds.Value;
                }

                response.Targets.Add(target);
            }
        }
        catch (AskTimeoutException)
        {
            _logger.LogWarning("SchedulerActor did not respond within {Timeout}s — returning empty trigger targets", AskTimeout.TotalSeconds);
        }

        return response;
    }

    // ═══════════════════════════════════════
    // Trigger
    // ═══════════════════════════════════════

    public override async Task<TriggerPollResponse> TriggerPoll(TriggerPollRequest request, ServerCallContext context)
    {
        var scheduler = _actorRegistry.Get<SchedulerActor>();
        var result = await scheduler.Ask<TriggerPollResult>(
            new TriggerImmediatePoll(request.Location, request.Model), AskTimeout);

        var response = new TriggerPollResponse { TriggeredCount = result.Count };
        response.Targets.AddRange(result.Targets);
        return response;
    }
}
