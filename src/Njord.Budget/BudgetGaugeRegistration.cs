using Akka.Actor;
using Akka.Hosting;
using Microsoft.Extensions.Hosting;
using Njord.Core.Actors;
using Njord.Core.Configuration;
using Njord.Core.Configuration.Polling;
using Njord.Messages.Budget;

namespace Njord.Budget;

internal sealed class BudgetGaugeRegistration(ActorRegistry actorRegistry, ConfigView config) : BackgroundService
{
    private static readonly TimeSpan RefreshInterval = TimeSpan.FromSeconds(5);
    private static readonly TimeSpan AskTimeout = TimeSpan.FromSeconds(3);

    private long _dailyUsed;
    private long _monthlyUsed;

    public override Task StartAsync(CancellationToken cancellationToken)
    {
        BudgetTelemetry.Observe(
            () => Volatile.Read(ref _dailyUsed),
            () => Volatile.Read(ref _monthlyUsed),
            () => config.Polling.ToBudgetAllowance().Daily,
            () => config.Polling.ToBudgetAllowance().Monthly);
        return base.StartAsync(cancellationToken);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            await RefreshAsync(stoppingToken);
            await Task.Delay(RefreshInterval, stoppingToken);
        }
    }

    internal async Task RefreshAsync(CancellationToken cancellationToken)
    {
        if (!actorRegistry.TryGet<IBudgetTracker>(out var budgetTracker))
        {
            return;
        }

        try
        {
            var result = await budgetTracker.Ask<BudgetStatusResult>(
                new QueryBudgetStatus(), AskTimeout, cancellationToken);
            Volatile.Write(ref _dailyUsed, result.DailyUsed);
            Volatile.Write(ref _monthlyUsed, result.MonthlyUsed);
        }
        catch (AskTimeoutException)
        {
            // Actor unresponsive — gauges keep their last-known values.
        }
    }
}
