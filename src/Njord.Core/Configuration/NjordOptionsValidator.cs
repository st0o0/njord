using Microsoft.Extensions.Options;

namespace Njord.Core.Configuration;

public sealed class NjordOptionsValidator : IValidateOptions<NjordOptions>
{
    public ValidateOptionsResult Validate(string? name, NjordOptions options)
    {
        if (options.Locations.Count == 0)
        {
            return ValidateOptionsResult.Fail("At least one location (Name; Latitude and Longitude optional, resolved by geocoding) is required.");
        }

        return options.Locations.Any(l => string.IsNullOrWhiteSpace(l.Name))
            ? ValidateOptionsResult.Fail("Every location needs a non-empty Name.")
            : ValidateOptionsResult.Success;
    }
}
