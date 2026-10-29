using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using Njord.Core.Configuration;
using Njord.Core.Domain;

namespace Njord.Polling.Configuration;

public sealed class PollingOptionsValidator(IConfiguration configuration, IOptions<FeatureRequirementsOptions> requirements) : IValidateOptions<PollingOptions>
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
            && BudgetGuard.Violation(scope, options, requirements.Value, ReadWeather(), ReadFlood()) is { } violation)
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
        var scope = configuration.GetSection(IConfigSection.SectionOf<NjordOptions>()).Get<NjordOptions>();
        return scope is null || scope.Locations.Count == 0 ? null : scope;
    }

    private WeatherOptions ReadWeather() =>
        configuration.GetSection(IConfigSection.SectionOf<WeatherOptions>()).Get<WeatherOptions>() ?? new WeatherOptions();

    private FloodOptions ReadFlood() =>
        configuration.GetSection(IConfigSection.SectionOf<FloodOptions>()).Get<FloodOptions>() ?? new FloodOptions();
}
