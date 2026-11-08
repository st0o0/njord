using Microsoft.Extensions.Options;
using Njord.Core.Configuration;
using Njord.Core.Domain;
using Njord.Core.Polling;
using Terp.Options;

namespace Njord.Polling.Configuration;

public sealed class PollingOptionsValidator(
    IOptions<NjordOptions> scope,
    IOptions<DomainCallConfig> callConfig,
    IOptions<DomainRequirements> requirements) : IValidateOptions<PollingOptions>
{
    public ValidateOptionsResult Validate(string? name, PollingOptions options)
    {
        var failures = new ValidationFailures();

        failures.Require(options.PollInterval > TimeSpan.Zero, "PollInterval must be positive.");
        failures.Require(options.DiscoveryInterval > TimeSpan.Zero, "DiscoveryInterval must be positive.");

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

        return failures.ToResult();
    }

    // A scope that fails its own validation is reported by NjordOptionsValidator instead of throwing here.
    private NjordOptions? ReadScope()
    {
        try
        {
            return scope.Value.ResolvedLocations.Count == 0 ? null : scope.Value;
        }
        catch (OptionsValidationException)
        {
            return null;
        }
    }
}
