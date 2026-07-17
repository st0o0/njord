using Microsoft.Extensions.Options;
using Njord.Domain.Weather;

namespace Njord.Configuration;

public sealed class NjordOptionsValidator : IValidateOptions<NjordOptions>
{
    private const double MonthlyBudgetGuardFactor = 0.8;
    private const int FetchWindowHours = 96;
    private static readonly TimeSpan Month = TimeSpan.FromDays(30);

    public ValidateOptionsResult Validate(string? name, NjordOptions options)
    {
        var failures = new List<string>();

        if (options.Locations.Count == 0)
        {
            failures.Add("At least one location (Name, Latitude, Longitude) is required.");
        }
        else if (options.Locations.Any(l => string.IsNullOrWhiteSpace(l.Name)))
        {
            failures.Add("Every location needs a non-empty Name.");
        }

        if (options.Models.Count == 0)
        {
            failures.Add("At least one model id is required (e.g. icon_d2).");
        }
        else if (options.Models.Any(string.IsNullOrWhiteSpace))
        {
            failures.Add("Model ids must be non-empty strings.");
        }

        if (options.PollInterval <= TimeSpan.Zero)
        {
            failures.Add("PollInterval must be positive.");
        }

        if (options.DiscoveryInterval <= TimeSpan.Zero)
        {
            failures.Add("DiscoveryInterval must be positive.");
        }

        if (options.Horizons.Count == 0)
        {
            failures.Add("At least one forecast horizon (hours) is required.");
        }
        else if (options.Horizons.Any(h => h <= 0 || h > FetchWindowHours))
        {
            failures.Add($"Horizons must be between 1 and {FetchWindowHours} hours (the fetched forecast window).");
        }

        if (options.Persistence.Provider == PersistenceProvider.PostgreSql
            && string.IsNullOrWhiteSpace(options.Persistence.ConnectionString))
        {
            failures.Add("PostgreSQL persistence requires a connection string — set Njord:Persistence:ConnectionString.");
        }

        if (options.Mqtt.Enabled && string.IsNullOrWhiteSpace(options.Mqtt.Host))
        {
            failures.Add("The MQTT broker host is missing — set Njord:Mqtt:Host.");
        }

        ResolvedParameterSet? resolved = null;
        try
        {
            resolved = ParameterRegistry.Resolve(
                options.Parameters.Groups,
                options.Parameters.Extra,
                options.Parameters.Exclude);
        }
        catch (ParameterResolutionException ex)
        {
            failures.AddRange(ex.Errors);
        }

        if (resolved is not null && options.Locations.Count > 0 && options.Models.Count > 0 && options.PollInterval > TimeSpan.Zero)
        {
            var totalModelsPerCycle = 0;

            foreach (var location in options.Locations)
            {
                var effectiveModels = location.ResolveModels(options.Models);
                totalModelsPerCycle += effectiveModels.Count;

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

            var budget = options.EffectiveBudget;
            var cyclesPerMonth = Month.TotalMinutes / options.PollInterval.TotalMinutes;
            var projected = (int)Math.Round(totalModelsPerCycle * cyclesPerMonth * resolved.ApiCallWeight);
            var guard = (int)Math.Round(budget.RequestsPerMonth * MonthlyBudgetGuardFactor);
            if (projected > guard)
            {
                failures.Add(
                    $"Projected API usage of {projected} effective requests/month ({totalModelsPerCycle} model-location pairs " +
                    $"every {options.PollInterval.TotalMinutes:0} min, weight {resolved.ApiCallWeight}) exceeds the " +
                    $"guard of {guard} (80% of the {budget.RequestsPerMonth}/month budget). " +
                    "Reduce locations/models, increase PollInterval, or exclude parameters.");
            }
        }

        return failures.Count > 0
            ? ValidateOptionsResult.Fail(failures)
            : ValidateOptionsResult.Success;
    }
}
