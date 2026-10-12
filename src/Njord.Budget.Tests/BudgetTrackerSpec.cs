using Akka.Actor;
using Akka.Event;
using Akka.Hosting;
using Akka.Persistence;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Time.Testing;
using Njord.Core.Health;
using Njord.Messages.Pipeline;
using Njord.Tests.Shared;

namespace Njord.Budget.Tests;

public sealed class BudgetTrackerSpec : NjordActorSpec
{
    private static readonly DateTimeOffset T0 = TestTimeConstants.BudgetEpoch;
    private readonly FakeTimeProvider _time = TestTimeouts.AtEpoch(T0);
    private readonly NjordHealthState _healthState = new() { ServiceStartedUtc = T0 };

    protected override void ConfigureServices(HostBuilderContext context, IServiceCollection services)
    {
        services.AddSingleton<TimeProvider>(_time);
    }

    private IActorRef CreateActor(string? name = null)
    {
        return Sys.ActorOf(
            Props.Create(() => new BudgetTracker(_time, _healthState)),
            name ?? $"budget-tracker-{Guid.NewGuid():N}");
    }

    [Fact(Timeout = TestTimeouts.Hosted)]
    public async Task Records_and_queries_usage()
    {
        var actor = CreateActor();

        actor.Tell(new RecordApiCall(1));
        actor.Tell(new RecordApiCall(1));

        var usage = await actor.Ask<BudgetUsageResult>(
            new QueryBudgetUsage(), TimeSpan.FromSeconds(3), TestContext.Current.CancellationToken);

        Assert.Equal(2, usage.MonthlyUsed);
        Assert.Equal(2, usage.DailyUsed);
    }

    [Fact(Timeout = TestTimeouts.Hosted)]
    public async Task Weighted_calls_accumulate_correctly()
    {
        var actor = CreateActor();

        actor.Tell(new RecordApiCall(3));
        actor.Tell(new RecordApiCall(2));
        actor.Tell(new RecordApiCall(1));

        var usage = await actor.Ask<BudgetUsageResult>(
            new QueryBudgetUsage(), TimeSpan.FromSeconds(3), TestContext.Current.CancellationToken);

        Assert.Equal(6, usage.MonthlyUsed);
        Assert.Equal(6, usage.DailyUsed);
    }

    [Fact(Timeout = TestTimeouts.Hosted)]
    public async Task Day_boundary_resets_daily_but_not_monthly()
    {
        var actor = CreateActor();

        actor.Tell(new RecordApiCall(5));

        var usage1 = await actor.Ask<BudgetUsageResult>(
            new QueryBudgetUsage(), TimeSpan.FromSeconds(3), TestContext.Current.CancellationToken);
        Assert.Equal(5, usage1.DailyUsed);

        _time.SetUtcNow(T0.AddDays(1));

        actor.Tell(new RecordApiCall(2));

        var usage2 = await actor.Ask<BudgetUsageResult>(
            new QueryBudgetUsage(), TimeSpan.FromSeconds(3), TestContext.Current.CancellationToken);

        Assert.Equal(7, usage2.MonthlyUsed);
        Assert.Equal(2, usage2.DailyUsed);
    }

    [Fact(Timeout = TestTimeouts.Hosted)]
    public async Task Month_boundary_resets_both_counters()
    {
        var actor = CreateActor();

        actor.Tell(new RecordApiCall(10));

        var usage1 = await actor.Ask<BudgetUsageResult>(
            new QueryBudgetUsage(), TimeSpan.FromSeconds(3), TestContext.Current.CancellationToken);
        Assert.Equal(10, usage1.MonthlyUsed);

        _time.SetUtcNow(new DateTimeOffset(2026, 8, 1, 0, 0, 0, TimeSpan.Zero));

        actor.Tell(new RecordApiCall(3));

        var usage2 = await actor.Ask<BudgetUsageResult>(
            new QueryBudgetUsage(), TimeSpan.FromSeconds(3), TestContext.Current.CancellationToken);

        Assert.Equal(3, usage2.MonthlyUsed);
        Assert.Equal(3, usage2.DailyUsed);
    }

    [Fact(Timeout = TestTimeouts.Hosted)]
    public async Task Recovery_replays_events_from_current_month()
    {
        const string actorName = "recovery-test";
        var actor = CreateActor(actorName);

        actor.Tell(new RecordApiCall(4));
        actor.Tell(new RecordApiCall(3));

        var usage1 = await actor.Ask<BudgetUsageResult>(
            new QueryBudgetUsage(), TimeSpan.FromSeconds(3), TestContext.Current.CancellationToken);
        Assert.Equal(7, usage1.MonthlyUsed);

        await actor.GracefulStop(TimeSpan.FromSeconds(3));

        var recovered = CreateActor();

        var usage2 = await recovered.Ask<BudgetUsageResult>(
            new QueryBudgetUsage(), TimeSpan.FromSeconds(3), TestContext.Current.CancellationToken);

        Assert.Equal(7, usage2.MonthlyUsed);
        Assert.Equal(7, usage2.DailyUsed);
    }

    [Fact(Timeout = TestTimeouts.Hosted)]
    public async Task Recovery_skips_events_from_previous_month()
    {
        const string actorName = "stale-month-test";
        var actor = CreateActor(actorName);

        actor.Tell(new RecordApiCall(10));

        var usage1 = await actor.Ask<BudgetUsageResult>(
            new QueryBudgetUsage(), TimeSpan.FromSeconds(3), TestContext.Current.CancellationToken);
        Assert.Equal(10, usage1.MonthlyUsed);

        await actor.GracefulStop(TimeSpan.FromSeconds(3));

        _time.SetUtcNow(new DateTimeOffset(2026, 8, 1, 0, 0, 0, TimeSpan.Zero));

        var recovered = CreateActor();

        var usage2 = await recovered.Ask<BudgetUsageResult>(
            new QueryBudgetUsage(), TimeSpan.FromSeconds(3), TestContext.Current.CancellationToken);

        Assert.Equal(0, usage2.MonthlyUsed);
        Assert.Equal(0, usage2.DailyUsed);
    }

    [Fact(Timeout = TestTimeouts.Hosted)]
    public async Task Returns_zero_before_any_calls()
    {
        var actor = CreateActor();

        var usage = await actor.Ask<BudgetUsageResult>(
            new QueryBudgetUsage(), TimeSpan.FromSeconds(3), TestContext.Current.CancellationToken);

        Assert.Equal(0, usage.MonthlyUsed);
        Assert.Equal(0, usage.DailyUsed);
    }

    [Fact(Timeout = TestTimeouts.Hosted)]
    public async Task Delete_messages_failure_is_logged_as_warning()
    {
        var warnings = CreateTestProbe();
        Sys.EventStream.Subscribe(warnings, typeof(Warning));
        var actor = CreateActor();

        actor.Tell(new DeleteMessagesFailure(new InvalidOperationException("boom-messages"), 5));

        var warning = await warnings.FishForMessageAsync<Warning>(
            w => w.Message.ToString()!.Contains("Delete messages failed for budget-tracker"),
            TestTimeouts.AwaitAssertMax, cancellationToken: TestContext.Current.CancellationToken);
        Assert.Equal("boom-messages", warning.Cause.Message);
    }

    [Fact(Timeout = TestTimeouts.Hosted)]
    public async Task Delete_snapshots_failure_is_logged_as_warning()
    {
        var warnings = CreateTestProbe();
        Sys.EventStream.Subscribe(warnings, typeof(Warning));
        var actor = CreateActor();

        actor.Tell(new DeleteSnapshotsFailure(new SnapshotSelectionCriteria(4), new InvalidOperationException("boom-snapshots")));

        var warning = await warnings.FishForMessageAsync<Warning>(
            w => w.Message.ToString()!.Contains("Delete snapshots failed for budget-tracker"),
            TestTimeouts.AwaitAssertMax, cancellationToken: TestContext.Current.CancellationToken);
        Assert.Equal("boom-snapshots", warning.Cause.Message);
    }
}
