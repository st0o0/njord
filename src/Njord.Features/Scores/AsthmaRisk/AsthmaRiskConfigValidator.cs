using Microsoft.Extensions.Options;
using Njord.Core.Configuration;
using Terp.Options;

namespace Njord.Features.Scores.AsthmaRisk;

public sealed class AsthmaRiskConfigValidator : IValidateOptions<AsthmaRiskConfig>
{
    public ValidateOptionsResult Validate(string? name, AsthmaRiskConfig options)
    {
        var failures = new ValidationFailures(IConfigSection.SectionOf<AsthmaRiskConfig>());

        failures.Require(options.Pm25Weight >= 0, $"Pm25Weight must not be negative, got {options.Pm25Weight}.");
        failures.Require(options.OzoneWeight >= 0, $"OzoneWeight must not be negative, got {options.OzoneWeight}.");
        failures.Require(options.PollenWeight >= 0, $"PollenWeight must not be negative, got {options.PollenWeight}.");
        failures.Require(options.HumidityWeight >= 0, $"HumidityWeight must not be negative, got {options.HumidityWeight}.");
        failures.Require(options.BoundaryLayerWeight >= 0, $"BoundaryLayerWeight must not be negative, got {options.BoundaryLayerWeight}.");

        var sum = options.Pm25Weight + options.OzoneWeight + options.PollenWeight
                  + options.HumidityWeight + options.BoundaryLayerWeight;
        failures.Require(sum == 100, $" weights must sum to 100, got {sum}.");

        failures.Require(options.MinWindowScore >= 1, $"MinWindowScore must be at least 1, got {options.MinWindowScore}.");
        failures.Require(options.MinWindowHours >= 1, $"MinWindowHours must be at least 1, got {options.MinWindowHours}.");
        failures.Require(options.MaxWindows >= 1, $"MaxWindows must be at least 1, got {options.MaxWindows}.");

        return failures.ToResult();
    }
}
