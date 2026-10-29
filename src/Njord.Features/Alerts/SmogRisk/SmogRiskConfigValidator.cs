using Microsoft.Extensions.Options;
using Njord.Core.Configuration;

namespace Njord.Features.Alerts.SmogRisk;

public sealed class SmogRiskConfigValidator : IValidateOptions<SmogRiskConfig>
{
    public ValidateOptionsResult Validate(string? name, SmogRiskConfig options)
    {
        var section = IConfigSection.SectionOf<SmogRiskConfig>();
        var failures = new List<string>();

        if (options.BoundaryLayerThresholdM <= 0)
        {
            failures.Add($"{section}:BoundaryLayerThresholdM must be positive, got {options.BoundaryLayerThresholdM}.");
        }

        if (options.Pm25ThresholdUgm3 <= 0)
        {
            failures.Add($"{section}:Pm25ThresholdUgm3 must be positive, got {options.Pm25ThresholdUgm3}.");
        }

        return failures.Count == 0 ? ValidateOptionsResult.Success : ValidateOptionsResult.Fail(failures);
    }
}
