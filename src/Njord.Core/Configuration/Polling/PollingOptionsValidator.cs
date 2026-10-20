using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using Njord.Core.Domain;

namespace Njord.Core.Configuration.Polling;

public sealed class PollingOptionsValidator(IConfiguration configuration, ResolvedRequirements requirements) : IValidateOptions<PollingOptions>
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
            && ReadScope() is { } scope
            && BudgetCalculator.GuardViolation(
                BudgetCalculator.Validate(scope, ReadWeather(), options, requirements), options.PollInterval) is { } violation)
        {
            failures.Add(violation);
        }

        return failures.Count > 0
            ? ValidateOptionsResult.Fail(failures)
            : ValidateOptionsResult.Success;
    }

    // The projection needs the forecast scope; it is read from configuration (not from IOptions<NjordOptions>)
    // so a scope that fails its own validation is reported by NjordOptionsValidator instead of throwing here.
    private NjordOptions? ReadScope()
    {
        var scope = configuration.GetSection(NjordOptions.SectionName).Get<NjordOptions>();
        return scope is null || scope.Locations.Count == 0 ? null : scope;
    }

    private WeatherOptions ReadWeather() =>
        configuration.GetSection(WeatherOptions.SectionName).Get<WeatherOptions>() ?? new WeatherOptions();
}
