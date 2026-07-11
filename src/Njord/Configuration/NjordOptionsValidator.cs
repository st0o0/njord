using Microsoft.Extensions.Options;

namespace Njord.Configuration;

public sealed class NjordOptionsValidator : IValidateOptions<NjordOptions>
{
    // Startup guard: refuse configurations projected to burn more than this
    // share of the monthly budget, leaving headroom for restarts and probes.
    // The Open-Meteo limits are soft, but a free service deserves politeness.
    private const double MonthlyBudgetGuardFactor = 0.8;

    private static readonly TimeSpan Month = TimeSpan.FromDays(30);

    public ValidateOptionsResult Validate(string? name, NjordOptions options)
    {
        var failures = new List<string>();

        if (options.Locations.Count == 0)
            failures.Add("At least one location (Name, Latitude, Longitude) is required.");
        else if (options.Locations.Any(l => string.IsNullOrWhiteSpace(l.Name)))
            failures.Add("Every location needs a non-empty Name.");

        if (options.Models.Count == 0)
            failures.Add("At least one model id is required (e.g. icon_d2).");
        else if (options.Models.Any(string.IsNullOrWhiteSpace))
            failures.Add("Model ids must be non-empty strings.");

        if (options.PollInterval <= TimeSpan.Zero)
            failures.Add("PollInterval must be positive.");

        if (options.Locations.Count > 0 && options.Models.Count > 0 && options.PollInterval > TimeSpan.Zero)
        {
            var budget = options.EffectiveBudget;
            var cyclesPerMonth = Month.TotalMinutes / options.PollInterval.TotalMinutes;
            var projected = (int)Math.Round(options.Locations.Count * options.Models.Count * cyclesPerMonth);
            var guard = (int)Math.Round(budget.RequestsPerMonth * MonthlyBudgetGuardFactor);
            if (projected > guard)
            {
                failures.Add(
                    $"Projected API usage of {projected} requests/month ({options.Locations.Count} locations x " +
                    $"{options.Models.Count} models every {options.PollInterval.TotalMinutes:0} min) exceeds the " +
                    $"guard of {guard} (80% of the {budget.RequestsPerMonth}/month budget). " +
                    "Reduce locations/models or increase PollInterval.");
            }
        }

        return failures.Count > 0
            ? ValidateOptionsResult.Fail(failures)
            : ValidateOptionsResult.Success;
    }
}
