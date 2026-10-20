using Microsoft.Extensions.Options;

namespace Njord.Core.Configuration;

public sealed class SolarYieldOptionsValidator : IValidateOptions<SolarYieldOptions>
{
    public ValidateOptionsResult Validate(string? name, SolarYieldOptions options)
    {
        var errors = new List<string>();
        if (options.PeakKw <= 0)
        {
            errors.Add($"SolarYield.PeakKw must be positive, got {options.PeakKw}");
        }

        if (options.Azimuth is < 0 or > 360)
        {
            errors.Add($"SolarYield.Azimuth must be between 0 and 360, got {options.Azimuth}");
        }

        if (options.Tilt is < 0 or > 90)
        {
            errors.Add($"SolarYield.Tilt must be between 0 and 90, got {options.Tilt}");
        }

        return errors.Count > 0 ? ValidateOptionsResult.Fail(errors) : ValidateOptionsResult.Success;
    }
}
