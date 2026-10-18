using Microsoft.Extensions.Options;

namespace Njord.Core.Configuration.Health;

public sealed class HealthOptionsValidator : IValidateOptions<HealthOptions>
{
    public ValidateOptionsResult Validate(string? name, HealthOptions options)
    {
        var failures = new List<string>();

        if (options.PollOverdueFactor <= 1)
        {
            failures.Add($"Health.PollOverdueFactor must be greater than 1, got {options.PollOverdueFactor}");
        }

        if (options.PollStalledFactor <= options.PollOverdueFactor)
        {
            failures.Add(
                $"Health.PollStalledFactor must be greater than PollOverdueFactor ({options.PollOverdueFactor}), got {options.PollStalledFactor}");
        }

        if (options.MqttUnhealthyAfter <= TimeSpan.Zero)
        {
            failures.Add($"Health.MqttUnhealthyAfter must be positive, got {options.MqttUnhealthyAfter}");
        }

        if (options.BudgetDegradedBelowPercent is <= 0 or >= 100)
        {
            failures.Add(
                $"Health.BudgetDegradedBelowPercent must be between 0 and 100 exclusive, got {options.BudgetDegradedBelowPercent}");
        }

        return failures.Count == 0 ? ValidateOptionsResult.Success : ValidateOptionsResult.Fail(failures);
    }
}
