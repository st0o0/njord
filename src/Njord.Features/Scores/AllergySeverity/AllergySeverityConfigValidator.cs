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
        failures.Require(options.PollenReference > 0, $"PollenReference must be positive, got {options.PollenReference}.");
        failures.Require(options.GoodHourScore > 0, $"GoodHourScore must be positive, got {options.GoodHourScore}.");
        failures.Require(options.WindThreshold > 0, $"WindThreshold must be positive, got {options.WindThreshold}.");
        failures.Require(options.BoundaryLayerThreshold > 0, $"BoundaryLayerThreshold must be positive, got {options.BoundaryLayerThreshold}.");

        return failures.ToResult();
    }
}
