using Microsoft.Extensions.Options;
using Njord.Weather;

namespace Njord.Core.Configuration;

public sealed class NjordOptionsValidator : IValidateOptions<NjordOptions>
{
    private const double MonthlyBudgetGuardFactor = 0.8;
    private const int FetchWindowHours = 96;

    public ValidateOptionsResult Validate(string? name, NjordOptions options)
    {
        var failures = new List<string>();

        ValidateLocations(options, failures);
        ValidateModels(options, failures);
        ValidateIntervals(options, failures);
        ValidateHorizons(options, failures);
        ValidateConnections(options, failures);

        var parametersResolved = TryResolveParameters(options, failures);

        if (parametersResolved && options.Locations.Count > 0 && options.Models.Count > 0 && options.PollInterval > TimeSpan.Zero)
        {
            ValidateModelCoverage(options, failures);
            ValidateBudget(options, failures);
        }

        ValidatePersistenceWritable(options, failures);

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

    private static void ValidateIntervals(NjordOptions options, List<string> failures)
    {
        if (options.PollInterval <= TimeSpan.Zero)
        {
            failures.Add("PollInterval must be positive.");
        }

        if (options.DiscoveryInterval <= TimeSpan.Zero)
        {
            failures.Add("DiscoveryInterval must be positive.");
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

    private static void ValidateConnections(NjordOptions options, List<string> failures)
    {
        if (options.Persistence.Provider == PersistenceProvider.PostgreSql
            && string.IsNullOrWhiteSpace(options.Persistence.ConnectionString))
        {
            failures.Add("PostgreSQL persistence requires a connection string — set Njord:Persistence:ConnectionString.");
        }

        if (options.Mqtt.Enabled && string.IsNullOrWhiteSpace(options.Mqtt.Host))
        {
            failures.Add("The MQTT broker host is missing — set Njord:Mqtt:Host.");
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
                        $"({location.Latitude:F2}°N, {location.Longitude:F2}°E) — " +
                        $"documented coverage: {coverage!.Region}" +
                        (coverage.Bounds is { } b ? $" ({b.MinLat}–{b.MaxLat}°N, {b.MinLon}–{b.MaxLon}°E)" : "") +
                        ". Remove this model from the location or global list if it does not return data.");
                }
                // Unknown models are allowed — forward compatible with new Open-Meteo models
            }
        }
    }

    private static void ValidateBudget(NjordOptions options, List<string> failures)
    {
        var projection = BudgetCalculator.Validate(options);
        if (projection.UsagePercent <= MonthlyBudgetGuardFactor * 100)
        {
            return;
        }

        var guard = (int)Math.Round(projection.MonthlyLimit * MonthlyBudgetGuardFactor);
        failures.Add(
            $"Projected API usage of {projection.ProjectedMonthlyCalls} effective requests/month ({projection.TotalModelsPerCycle} model-location pairs " +
            $"every {options.PollInterval.TotalMinutes:0} min, weight {projection.ApiCallWeight}) exceeds the " +
            $"guard of {guard} (80% of the {projection.MonthlyLimit}/month budget). " +
            "Reduce locations/models, increase PollInterval, or exclude parameters.");
    }

    private static void ValidatePersistenceWritable(NjordOptions options, List<string> failures)
    {
        if (options.Persistence is not { Provider: PersistenceProvider.Sqlite, ConnectionString: null })
        {
            return;
        }

        var dir = Path.GetDirectoryName(options.PersistencePath);
        if (string.IsNullOrEmpty(dir))
        {
            return;
        }

        try
        {
            Directory.CreateDirectory(dir);
            var probe = Path.Combine(dir, ".njord-write-probe");
            File.WriteAllBytes(probe, []);
            File.Delete(probe);
        }
        catch (Exception ex)
        {
            var fullDir = Path.GetFullPath(dir);
            failures.Add(
                $"Persistence directory '{fullDir}' is not writable: {ex.Message}. " +
                "Ensure the volume is mounted and the container user has write access.");
        }
    }
}
