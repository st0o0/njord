using Microsoft.Extensions.Hosting;
using Njord.Budget.Health;
using Njord.Core.Configuration;
using Njord.Core.Configuration.Polling;

namespace Njord.Budget;

internal sealed class BudgetGaugeRegistration(BudgetUsageState usage, ConfigView config) : IHostedService
{
    public Task StartAsync(CancellationToken cancellationToken)
    {
        BudgetTelemetry.Observe(
            () => usage.Current.Daily,
            () => usage.Current.Monthly,
            () => config.Polling.ToBudgetAllowance().Daily,
            () => config.Polling.ToBudgetAllowance().Monthly);
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
