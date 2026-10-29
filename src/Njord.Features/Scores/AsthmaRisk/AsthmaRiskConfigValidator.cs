using Microsoft.Extensions.Options;
using Njord.Core.Configuration;

namespace Njord.Features.Scores.AsthmaRisk;

public sealed class AsthmaRiskConfigValidator : IValidateOptions<AsthmaRiskConfig>
{
    public ValidateOptionsResult Validate(string? name, AsthmaRiskConfig options)
    {
        var section = IConfigSection.SectionOf<AsthmaRiskConfig>();
        var failures = new List<string>();

        if (options.Pm25Weight < 0)
            failures.Add($"{section}:Pm25Weight must not be negative, got {options.Pm25Weight}.");
        if (options.OzoneWeight < 0)
            failures.Add($"{section}:OzoneWeight must not be negative, got {options.OzoneWeight}.");
        if (options.PollenWeight < 0)
            failures.Add($"{section}:PollenWeight must not be negative, got {options.PollenWeight}.");
        if (options.HumidityWeight < 0)
            failures.Add($"{section}:HumidityWeight must not be negative, got {options.HumidityWeight}.");
        if (options.BoundaryLayerWeight < 0)
            failures.Add($"{section}:BoundaryLayerWeight must not be negative, got {options.BoundaryLayerWeight}.");

        var sum = options.Pm25Weight + options.OzoneWeight + options.PollenWeight
                  + options.HumidityWeight + options.BoundaryLayerWeight;
        if (sum != 100)
            failures.Add($"{section}: weights must sum to 100, got {sum}.");

        if (options.MinWindowScore < 1)
            failures.Add($"{section}:MinWindowScore must be at least 1, got {options.MinWindowScore}.");
        if (options.MinWindowHours < 1)
            failures.Add($"{section}:MinWindowHours must be at least 1, got {options.MinWindowHours}.");
        if (options.MaxWindows < 1)
            failures.Add($"{section}:MaxWindows must be at least 1, got {options.MaxWindows}.");

        return failures.Count == 0 ? ValidateOptionsResult.Success : ValidateOptionsResult.Fail(failures);
    }
}
