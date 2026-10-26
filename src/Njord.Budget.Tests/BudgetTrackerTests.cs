using Akka.Actor;
using Akka.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;
using Njord.Core.Configuration;
using Njord.Core.Polling;
using Njord.Messages.Budget;
using Njord.Tests.Shared;

namespace Njord.Budget.Tests;

public sealed class BudgetTrackerTests : Akka.Hosting.TestKit.TestKit
{
    private readonly FakeTimeProvider _time = new FakeTimeProvider(TestTimeConstants.BudgetEpoch);
    private IOptions<PollingOptions> _polling = Options.Create(AppSettingsDefaults.Options<PollingOptions>());

    protected override void ConfigureAkka(AkkaConfigurationBuilder builder, IServiceProvider provider) =>
        builder.AddTestPersistence();

    protected override void ConfigureServices(HostBuilderContext context, IServiceCollection services) =>
        services.AddSingleton<TimeProvider>(_time);

    private IActorRef CreateTracker() =>
        Sys.ActorOf(Props.Create(() => new BudgetTracker(_time, _polling)), $"budget-tracker-{Guid.NewGuid():N}");

    private async Task<BudgetUsageResult> UsageOf(IActorRef tracker, CancellationToken ct) =>
        await tracker.Ask<BudgetUsageResult>(new QueryBudgetUsage(), TimeSpan.FromSeconds(5), ct);

    private async Task StopAsync(IActorRef tracker)
    {
        Watch(tracker);
        Sys.Stop(tracker);
        await ExpectTerminatedAsync(tracker, cancellationToken: TestContext.Current.CancellationToken);
    }

    [Fact(Timeout = 30000)]
    public async Task Recorded_calls_are_reported_as_weighted_usage()
    {
        var tracker = CreateTracker();

        tracker.Tell(new RecordApiCall(4));
        tracker.Tell(new RecordApiCall(2));

        Assert.Equal(new BudgetUsageResult(6, 6), await UsageOf(tracker, TestContext.Current.CancellationToken));
    }

    [Fact(Timeout = 30000)]
    public async Task Status_pairs_the_usage_with_the_effective_limits()
    {
        _polling = Options.Create(AppSettingsDefaults.Options<PollingOptions>(o => o.BudgetOverride = new RequestBudget(250_000, 60)));
        var tracker = CreateTracker();

        tracker.Tell(new RecordApiCall(2));

        var status = await tracker.Ask<BudgetStatusResponse>(new QueryBudgetStatus(), TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);

        Assert.Equal(new BudgetStatusResult(MonthlyUsed: 2, DailyUsed: 2, MonthlyLimit: 250_000, DailyLimit: 10_000), status);
    }

    [Fact(Timeout = 30000)]
    public async Task A_new_day_resets_only_the_daily_counter()
    {
        var tracker = CreateTracker();
        tracker.Tell(new RecordApiCall(5));
        await UsageOf(tracker, TestContext.Current.CancellationToken);

        _time.Advance(TimeSpan.FromDays(1));
        tracker.Tell(new RecordApiCall(2));

        Assert.Equal(new BudgetUsageResult(MonthlyUsed: 7, DailyUsed: 2), await UsageOf(tracker, TestContext.Current.CancellationToken));
    }

    [Fact(Timeout = 30000)]
    public async Task A_restarted_tracker_recovers_usage_from_the_journal()
    {
        var tracker = CreateTracker();
        tracker.Tell(new RecordApiCall(4));
        tracker.Tell(new RecordApiCall(3));
        await UsageOf(tracker, TestContext.Current.CancellationToken);
        await StopAsync(tracker);

        var recovered = CreateTracker();

        Assert.Equal(new BudgetUsageResult(7, 7), await UsageOf(recovered, TestContext.Current.CancellationToken));
    }

    [Fact(Timeout = 30000)]
    public async Task Recovery_after_a_month_change_drops_the_previous_months_usage()
    {
        var tracker = CreateTracker();
        tracker.Tell(new RecordApiCall(10));
        await UsageOf(tracker, TestContext.Current.CancellationToken);
        await StopAsync(tracker);
        _time.SetUtcNow(new DateTimeOffset(2026, 8, 1, 0, 0, 0, TimeSpan.Zero));

        var recovered = CreateTracker();

        Assert.Equal(new BudgetUsageResult(0, 0), await UsageOf(recovered, TestContext.Current.CancellationToken));
    }
}
