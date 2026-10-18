using Microsoft.Extensions.Hosting;
using Njord.Core.Health;

namespace Njord.Budget;

internal sealed class BudgetGaugeRegistration(NjordHealthMonitor monitor) : IHostedService
{
    public Task StartAsync(CancellationToken cancellationToken)
    {
        BudgetTelemetry.Observe(
            () => monitor.Budget.UsedDaily,
            () => monitor.Budget.UsedMonthly,
            () => monitor.Budget.LimitDaily,
            () => monitor.Budget.LimitMonthly);
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
