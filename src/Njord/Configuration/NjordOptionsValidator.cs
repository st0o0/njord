using Microsoft.Extensions.Options;
using Njord.Domain;

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

        if (string.IsNullOrWhiteSpace(options.Mqtt.Host))
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
            var budget = options.EffectiveBudget;
            var cyclesPerMonth = Month.TotalMinutes / options.PollInterval.TotalMinutes;
            var projected = (int)Math.Round(options.Locations.Count * options.Models.Count * cyclesPerMonth * resolved.ApiCallWeight);
            var guard = (int)Math.Round(budget.RequestsPerMonth * MonthlyBudgetGuardFactor);
            if (projected > guard)
            {
                failures.Add(
                    $"Projected API usage of {projected} effective requests/month ({options.Locations.Count} locations x " +
                    $"{options.Models.Count} models every {options.PollInterval.TotalMinutes:0} min, weight {resolved.ApiCallWeight}) exceeds the " +
                    $"guard of {guard} (80% of the {budget.RequestsPerMonth}/month budget). " +
                    "Reduce locations/models, increase PollInterval, or exclude parameters.");
            }
        }

        return failures.Count > 0
            ? ValidateOptionsResult.Fail(failures)
            : ValidateOptionsResult.Success;
    }
}
