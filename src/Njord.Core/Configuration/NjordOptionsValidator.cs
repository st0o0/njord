using Microsoft.Extensions.Options;
using Njord.Model;

namespace Njord.Core.Configuration;

public sealed class NjordOptionsValidator : IValidateOptions<NjordOptions>
{
    public ValidateOptionsResult Validate(string? name, NjordOptions options)
    {
        List<string> failures = [];

        ValidateLocations(options, failures);
        ValidateModels(options, failures);
        ValidateForecastDays(options, failures);
        ValidateEnabledDomains(options, failures);

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
            failures.Add("At least one location (Name, Latitude, Longitude) is required.");
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

    private static void ValidateEnabledDomains(NjordOptions options, List<string> failures)
    {
        if (!options.EnabledDomains.Contains(DomainKind.Weather))
        {
            failures.Add("EnabledDomains must include Weather.");
        }
        else if (options.EnabledDomains.Distinct().Count() != options.EnabledDomains.Count)
        {
            failures.Add("EnabledDomains must not contain duplicates.");
        }
        else if (options.EnabledDomains.Any(d => !Enum.IsDefined(d)))
        {
            failures.Add("EnabledDomains contains an unknown domain.");
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
