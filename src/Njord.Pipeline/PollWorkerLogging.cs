using Akka.Event;
using Njord.Core;
using Njord.Messages.Forecast;
using Njord.Messages.Pipeline;
using Njord.Model;
using Njord.Persistence.Events.Polling;

namespace Njord.Pipeline;

internal static class PollWorkerLogging
{
    public static void LogDomainOutcome(
        this ILoggingAdapter log, PollWorkerState state, DomainKind domain, FetchDomainResponse reply)
    {
        if (state.Target is not { } target)
        {
            return;
        }

        var ds = state.GetDomain(domain);
        switch (reply)
        {
            case FetchDomainCompleted completed when completed.Hash != ds.LastHash:
                log.Debug("Data changed for {Location}/{Domain} - phase={Phase}, cycle={Cycle}",
                    target.Location, domain, ds.Phase, ds.LearnedCycle);
                break;
            case FetchDomainCompleted:
                log.Debug("Hash unchanged for {Location}/{Domain} - miss={Miss}, phase={Phase}",
                    target.Location, domain, ds.MissCount, ds.Phase);
                break;
            case FetchDomainFailed { Reason: PollFailureReason.Transport } failed:
                log.Warning("Fetch failed for {Location}/{Domain} (transport) - details={Details}",
                    target.Location, domain, failed.Detail);
                break;
            case FetchDomainFailed { Reason: PollFailureReason.RateLimited }:
                log.Warning("Fetch rate-limited for {Location}/{Domain}",
                    target.Location, domain);
                break;
            case FetchDomainFailed failed:
                log.Warning("Fetch failed for {Location}/{Domain} ({Reason}: {Detail}) - retry scheduled",
                    target.Location, domain, failed.Reason, failed.Detail);
                break;
            case FetchDomainRejected rejected:
                log.Warning("Fetch rejected for {Location}/{Domain} ({Reason})",
                    target.Location, domain, rejected.Reason);
                break;
        }
    }

    public static void RecordDomainPoll(
        this ILoggingAdapter log, PollWorkerState state, DomainPolled polled, DomainPollState? before)
    {
        var domain = (DomainKind)polled.Domain;
        Telemetry.RecordDomainPoll(domain, polled.Outcome);
        if (polled.Outcome == 0 && before is { Phase: PollPhase.Discovery } && state.GetDomain(domain).Phase == PollPhase.Steady)
        {
            Telemetry.RecordCycleLearned(domain);
            log.Info("Learned update cycle {Cycle} for {Location}/{Domain}",
                state.GetDomain(domain).LearnedCycle, state.Target?.Location, domain);
        }
    }
}
