using Microsoft.Extensions.Options;

namespace Njord.Core.Configuration;

public sealed class NjordOptionsValidator : IValidateOptions<NjordOptions>
{
    public ValidateOptionsResult Validate(string? name, NjordOptions options)
    {
        if (options.Locations.Count == 0)
        {
            return ValidateOptionsResult.Fail("At least one location (Name, Latitude and Longitude) is required.");
        }

        if (options.Locations.Any(l => string.IsNullOrWhiteSpace(l.Name)))
        {
            return ValidateOptionsResult.Fail("Every location needs a non-empty Name.");
        }

        foreach (var location in options.Locations)
        {
            if (location.Latitude is null)
            {
                return ValidateOptionsResult.Fail($"Location '{location.Name}' needs a Latitude.");
            }

            if (location.Longitude is null)
            {
                return ValidateOptionsResult.Fail($"Location '{location.Name}' needs a Longitude.");
            }

            if (location.Latitude is < -90 or > 90)
            {
                return ValidateOptionsResult.Fail($"Location '{location.Name}' has a Latitude outside -90..90.");
            }

            if (location.Longitude is < -180 or > 180)
            {
                return ValidateOptionsResult.Fail($"Location '{location.Name}' has a Longitude outside -180..180.");
            }
        }

        return ValidateOptionsResult.Success;
    }
}
