using Microsoft.Extensions.Options;
using Njord.Core.Configuration;
using Terp.Options;

namespace Njord.Features.Scores.AllergySeverity;

public sealed class AllergySeverityConfigValidator : IValidateOptions<AllergySeverityConfig>
{
    public ValidateOptionsResult Validate(string? name, AllergySeverityConfig options)
    {
        var failures = new ValidationFailures(IConfigSection.SectionOf<AllergySeverityConfig>());

        failures.Require(options.MinWindowScore >= 1, $"MinWindowScore must be at least 1, got {options.MinWindowScore}.");
        failures.Require(options.MinWindowHours >= 1, $"MinWindowHours must be at least 1, got {options.MinWindowHours}.");
        failures.Require(options.MaxWindows >= 1, $"MaxWindows must be at least 1, got {options.MaxWindows}.");

        return failures.ToResult();
    }
}
