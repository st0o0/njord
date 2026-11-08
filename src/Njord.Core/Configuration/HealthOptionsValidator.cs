using Microsoft.Extensions.Options;
using Terp.Options;

namespace Njord.Core.Configuration;

public sealed class HealthOptionsValidator : IValidateOptions<HealthOptions>
{
    public ValidateOptionsResult Validate(string? name, HealthOptions options)
    {
        var failures = new ValidationFailures();

        failures.Require(options.PollOverdueFactor > 1,
            $"Health.PollOverdueFactor must be greater than 1, got {options.PollOverdueFactor}");

        if (options.PollStalledFactor <= options.PollOverdueFactor)
        {
            failures.Add(
                $"Health.PollStalledFactor must be greater than PollOverdueFactor ({options.PollOverdueFactor}), got {options.PollStalledFactor}");
        }

        if (options.BudgetDegradedBelowPercent is <= 0 or >= 100)
        {
            failures.Add(
                $"Health.BudgetDegradedBelowPercent must be between 0 and 100 exclusive, got {options.BudgetDegradedBelowPercent}");
        }

        return failures.ToResult();
    }
}
