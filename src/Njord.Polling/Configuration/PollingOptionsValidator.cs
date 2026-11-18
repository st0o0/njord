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
    private static readonly TimeSpan _maxDomainInterval = TimeSpan.FromHours(24);

    public ValidateOptionsResult Validate(string? name, PollingOptions options)
    {
        var failures = new ValidationFailures();

        failures.Require(options.PollInterval > TimeSpan.Zero, "PollInterval must be positive.");

        foreach (var (domain, over) in options.Domains)
        {
            if (over.PollInterval <= TimeSpan.Zero)
            {
                failures.Add($"Domains:{domain}:PollInterval must be positive.");
            }
            else if (over.PollInterval > _maxDomainInterval)
            {
                failures.Add($"Domains:{domain}:PollInterval must not exceed 24 hours.");
            }
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

        return failures.ToResult();
    }

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
