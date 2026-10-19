using Njord.Messages.Forecast;
using Njord.Model;

namespace Njord.Pipeline.Tests;

public sealed class PollRoundStateSpec
{
    private static readonly Location Lucerne = new("Lucerne", 0, 0);
    private static readonly Guid Round = Guid.NewGuid();
    private static readonly DomainKind[] Domains = [DomainKind.Weather, DomainKind.Flood];

    private static FetchDomainCompleted Completed(DomainKind domain, int hash) => new(Lucerne, domain, hash);

    [Fact]
    public void A_new_worker_has_no_round_in_flight()
    {
        Assert.Null(PollWorkerState.Empty.Round);
    }

    [Fact]
    public void Starting_a_round_marks_every_domain_pending()
    {
        var state = PollWorkerState.Empty.StartRound(Round, Domains);

        Assert.NotNull(state.Round);
        Assert.Multiple(
            () => Assert.True(state.IsCurrentRound(Round)),
            () => Assert.False(state.IsCurrentRound(Guid.NewGuid())),
            () => Assert.Equal(Domains.Order(), state.Round.Pending.Order()),
            () => Assert.Empty(state.Round.Results));
    }

    [Fact]
    public void A_reply_moves_its_domain_from_pending_to_results_and_a_stale_reply_is_ignored()
    {
        var started = PollWorkerState.Empty.StartRound(Round, Domains);

        var replied = started.RecordReply(Round, DomainKind.Weather, Completed(DomainKind.Weather, 7));
        var stale = replied.RecordReply(Guid.NewGuid(), DomainKind.Flood, Completed(DomainKind.Flood, 9));

        Assert.NotNull(replied.Round);
        Assert.Multiple(
            () => Assert.Equal([DomainKind.Flood], replied.Round.Pending),
            () => Assert.Equal(Completed(DomainKind.Weather, 7), replied.Round.Results[DomainKind.Weather]),
            () => Assert.Equal(replied, stale));
    }

    [Fact]
    public void Failing_the_pending_domains_completes_the_round_results()
    {
        var state = PollWorkerState.Empty.StartRound(Round, Domains)
            .RecordReply(Round, DomainKind.Weather, Completed(DomainKind.Weather, 7))
            .FailPending(domain => new FetchDomainFailed(Lucerne, domain, PollFailureReason.Transport, "timeout"));

        Assert.NotNull(state.Round);
        Assert.Multiple(
            () => Assert.Empty(state.Round.Pending),
            () => Assert.Equal(2, state.Round.Results.Count),
            () => Assert.IsType<FetchDomainFailed>(state.Round.Results[DomainKind.Flood]),
            () => Assert.Null(state.EndRound().Round));
    }

    [Fact]
    public void The_aggregate_reply_prefers_a_changed_domain_then_any_success_then_the_most_severe_failure()
    {
        var state = PollWorkerState.Empty.WithDomainHash(DomainKind.Weather, 1);
        var unchanged = Completed(DomainKind.Weather, 1);
        var changed = Completed(DomainKind.Flood, 2);
        var rateLimited = new FetchDomainFailed(Lucerne, DomainKind.AirQuality, PollFailureReason.RateLimited, "429");
        var transport = new FetchDomainFailed(Lucerne, DomainKind.Flood, PollFailureReason.Transport, "x");

        Assert.Multiple(
            () => Assert.Equal(changed, state.ChooseAggregateReply([unchanged, changed, rateLimited])),
            () => Assert.Equal(unchanged, state.ChooseAggregateReply([rateLimited, unchanged])),
            () => Assert.Equal(rateLimited, state.ChooseAggregateReply([transport, rateLimited])),
            () => Assert.Equal(transport, state.ChooseAggregateReply([transport])));
    }
}
