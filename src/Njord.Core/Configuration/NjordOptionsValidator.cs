using Microsoft.Extensions.Options;
using Njord.Weather;

namespace Njord.Core.Configuration;

public sealed class NjordOptionsValidator : IValidateOptions<NjordOptions>
{
    private const int FetchWindowHours = 96;

    public ValidateOptionsResult Validate(string? name, NjordOptions options)
    {
        var failures = new List<string>();

        ValidateLocations(options, failures);
        ValidateModels(options, failures);
        ValidateHorizons(options, failures);
        ValidateForecastDays(options, failures);

        var parametersResolved = TryResolveParameters(options, failures);

        if (parametersResolved && options.Locations.Count > 0 && options.Models.Count > 0)
        {
            ValidateModelCoverage(options, failures);
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
        if (options.Models.Count == 0)
        {
            failures.Add("At least one model id is required (e.g. icon_d2).");
        }
        else if (options.Models.Any(string.IsNullOrWhiteSpace))
        {
            failures.Add("Model ids must be non-empty strings.");
        }
    }

    private static void ValidateHorizons(NjordOptions options, List<string> failures)
    {
        if (options.Horizons.Count == 0)
        {
            failures.Add("At least one forecast horizon (hours) is required.");
        }
        else if (options.Horizons.Any(h => h <= 0 || h > FetchWindowHours))
        {
            failures.Add($"Horizons must be between 1 and {FetchWindowHours} hours (the fetched forecast window).");
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
            ParameterRegistry.Resolve(
                options.Parameters.Groups,
                options.Parameters.Extra,
                options.Parameters.Exclude);
            return true;
        }
        catch (ParameterResolutionException ex)
        {
            failures.AddRange(ex.Errors);
            return false;
        }
    }

    private static void ValidateModelCoverage(NjordOptions options, List<string> failures)
    {
        foreach (var location in options.Locations)
        {
            var effectiveModels = options.Models.Union(location.Models ?? [], StringComparer.OrdinalIgnoreCase);

            foreach (var modelId in effectiveModels)
            {
                if (!ModelCoverageRegistry.IsPlausible(modelId, location.Latitude, location.Longitude))
                {
                    var coverage = ModelCoverageRegistry.Get(modelId);
                    failures.Add(
                        $"Model '{modelId}' may not cover location '{location.Name}' " +
                        $"({location.Latitude:F2}°N, {location.Longitude:F2}°E): " +
                        $"documented coverage: {coverage!.Region}" +
                        (coverage.Bounds is { } b ? $" ({b.MinLat}–{b.MaxLat}°N, {b.MinLon}–{b.MaxLon}°E)" : "") +
                        ". Remove this model from the location or global list if it does not return data.");
                }
                // Unknown models are allowed: forward compatible with new Open-Meteo models
            }
        }
    }
}
