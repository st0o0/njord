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
        failures.Require(options.RedBoundaryLayerM > 0 && options.RedBoundaryLayerM < options.OrangeBoundaryLayerM,
            $"RedBoundaryLayerM must be positive and < OrangeBoundaryLayerM, got {options.RedBoundaryLayerM}, {options.OrangeBoundaryLayerM}.");
        failures.Require(options.RedPm25Ugm3 > options.OrangePm25Ugm3,
            $"RedPm25Ugm3 must be > OrangePm25Ugm3, got {options.RedPm25Ugm3}, {options.OrangePm25Ugm3}.");
        failures.Require(options.OrangePm25Ugm3 > 0, $"OrangePm25Ugm3 must be positive, got {options.OrangePm25Ugm3}.");

        return failures.ToResult();
    }
}
