using Microsoft.Extensions.Options;

namespace Njord.OpenMeteo.Configuration;

public sealed class SolarOptionsValidator : IValidateOptions<SolarOptions>
{
    public ValidateOptionsResult Validate(string? name, SolarOptions options)
    {
        List<string> failures = [];

        if (options.Tilt is < 0 or > 90)
            failures.Add($"Solar.Tilt must be between 0 and 90, got {options.Tilt}");

        if (options.Azimuth is < 0 or > 360)
            failures.Add($"Solar.Azimuth must be between 0 and 360, got {options.Azimuth}");

        return failures.Count > 0 ? ValidateOptionsResult.Fail(failures) : ValidateOptionsResult.Success;
    }
}
