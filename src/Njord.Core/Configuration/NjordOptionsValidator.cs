using Microsoft.Extensions.Options;

namespace Njord.Core.Configuration;

public sealed class NjordOptionsValidator : IValidateOptions<NjordOptions>
{
    public ValidateOptionsResult Validate(string? name, NjordOptions options)
    {
        List<string> failures = [];

        ValidateLocations(options, failures);
        ValidateModels(options, failures);
        ValidateForecastDays(options, failures);

        var parametersResolved = TryResolveParameters(options, failures);

        if (parametersResolved && options.Locations.Count > 0 && options.Models.Count > 0)
        {
        }

        return failures.Count > 0
            ? ValidateOptionsResult.Fail(failures)
            : ValidateOptionsResult.Success;
    }

    private static void ValidateLocations(NjordOptions options, List<string> failures)
    {
        if (options.Locations.Count == 0)
        {
            failures.Add("At least one location (Name; Latitude and Longitude optional, resolved by geocoding) is required.");
        }
        else if (options.Locations.Any(l => string.IsNullOrWhiteSpace(l.Name)))
        {
            failures.Add("Every location needs a non-empty Name.");
        }
    }

    private static void ValidateModels(NjordOptions options, List<string> failures)
    {
        if (options.Models.Any(string.IsNullOrWhiteSpace))
        {
            failures.Add("Model ids must be non-empty strings.");
        }
    }

    private static void ValidateForecastDays(NjordOptions options, List<string> failures)
    {
        if (options.ForecastDays is < 1 or > 16)
        {
            failures.Add("ForecastDays must be between 1 and 16.");
        }
    }

    private static bool TryResolveParameters(NjordOptions options, List<string> failures)
    {
        try
        {
            options.Parameters.Resolve();
            return true;
        }
        catch (ParameterResolutionException ex)
        {
            failures.AddRange(ex.Errors);
            return false;
        }
    }
}
