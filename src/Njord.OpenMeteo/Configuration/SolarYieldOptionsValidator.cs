using Microsoft.Extensions.Options;
using Njord.Core.Configuration;

namespace Njord.OpenMeteo.Configuration;

public sealed class SolarYieldOptionsValidator : IValidateOptions<SolarYieldOptions>
{
    public ValidateOptionsResult Validate(string? name, SolarYieldOptions options)
    {
        List<string> failures = [];

        if (options.PeakKw <= 0)
            failures.Add($"SolarYield.PeakKw must be positive, got {options.PeakKw}");

        if (options.SystemEfficiency is <= 0 or > 1)
            failures.Add($"SolarYield.SystemEfficiency must be greater than 0 and at most 1, got {options.SystemEfficiency}");

        if (options.Azimuth is < 0 or > 360)
            failures.Add($"SolarYield.Azimuth must be between 0 and 360, got {options.Azimuth}");

        if (options.Tilt is < 0 or > 90)
            failures.Add($"SolarYield.Tilt must be between 0 and 90, got {options.Tilt}");

        return failures.Count > 0 ? ValidateOptionsResult.Fail(failures) : ValidateOptionsResult.Success;
    }
}
