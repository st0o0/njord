using Microsoft.Extensions.Options;
using Njord.Core.Configuration;
using Njord.Core.Domain;
using Njord.Core.Polling;

namespace Njord.Polling.Configuration;

public sealed class PollingOptionsValidator(
    IOptions<NjordOptions> scope,
    IOptions<DomainCallConfig> callConfig,
    IOptions<DomainRequirements> requirements) : IValidateOptions<PollingOptions>
{
    public ValidateOptionsResult Validate(string? name, PollingOptions options)
    {
        var failures = new List<string>();

        if (options.PollInterval <= TimeSpan.Zero)
        {
            failures.Add("PollInterval must be positive.");
        }

        if (options.DiscoveryInterval <= TimeSpan.Zero)
        {
            failures.Add("DiscoveryInterval must be positive.");
        }

        if (options.BudgetOverride is { } budget && (budget.RequestsPerMonth <= 0 || budget.RequestsPerMinute <= 0))
        {
            failures.Add("BudgetOverride requests per month and per minute must be greater than zero.");
        }

        if (failures.Count == 0
            && ReadScope() is { } projectionScope
            && BudgetGuard.Violation(projectionScope, options, requirements.Value, callConfig.Value) is { } violation)
        {
            failures.Add(violation);
        }

        return failures.Count > 0
            ? ValidateOptionsResult.Fail(failures)
            : ValidateOptionsResult.Success;
    }

    // A scope that fails its own validation is reported by NjordOptionsValidator instead of throwing here.
    private NjordOptions? ReadScope()
    {
        try
        {
            return scope.Value.Locations.Count == 0 ? null : scope.Value;
        }
        catch (OptionsValidationException)
        {
            return null;
        }
    }
}
