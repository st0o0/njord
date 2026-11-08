using Microsoft.Extensions.Options;
using Njord.Core.Configuration;
using Terp.Options;

namespace Njord.Features.Alerts.SmogRisk;

public sealed class SmogRiskConfigValidator : IValidateOptions<SmogRiskConfig>
{
    public ValidateOptionsResult Validate(string? name, SmogRiskConfig options)
    {
        var failures = new ValidationFailures(IConfigSection.SectionOf<SmogRiskConfig>());

        failures.Require(options.BoundaryLayerThresholdM > 0, $"BoundaryLayerThresholdM must be positive, got {options.BoundaryLayerThresholdM}.");
        failures.Require(options.Pm25ThresholdUgm3 > 0, $"Pm25ThresholdUgm3 must be positive, got {options.Pm25ThresholdUgm3}.");

        return failures.ToResult();
    }
}
