using Njord.Core.Configuration;
using Njord.Core.Configuration.Polling;
using Njord.Messages.Config;

namespace Njord.Configuration;

internal sealed class ConfigUpdateHandler(IConfigWriter writer)
{
    public async Task<UpdateLocationsResponse> UpdateLocationsAsync(
        UpdateLocations cmd, NjordOptions scope, PollingOptions polling,
        Func<EffectiveConfig> currentConfig, CancellationToken ct)
    {
        if (cmd.Locations.Count == 0)
        {
            return new UpdateLocationsRejected("Cannot set empty location list");
        }

        List<LocationOptions> locations =
        [
            .. cmd.Locations.Select(l => new LocationOptions
            {
                Name = l.Name,
                Latitude = l.Latitude,
                Longitude = l.Longitude,
                Models = l.Models.Count > 0 ? [.. l.Models] : null,
            })
        ];

        if (BudgetRejection(s => s.Locations = locations, scope, polling) is { } budgetRejection)
        {
            return new UpdateLocationsRejected(budgetRejection);
        }

        var result = await writer.UpdateAsync<NjordOptions>(opt => opt.Locations = locations, ct);
        return result.Applied
            ? new UpdateLocationsCompleted(currentConfig(), [.. RestartRequired(result, "")])
            : new UpdateLocationsRejected(string.Join("; ", result.Failures));
    }

    public async Task<UpdateSettingsResponse> UpdateSettingsAsync(
        UpdateSettings cmd, NjordOptions scope, PollingOptions polling,
        Func<EffectiveConfig> currentConfig, CancellationToken ct)
    {
        if (cmd.PollInterval < TimeSpan.FromSeconds(60))
        {
            return new UpdateSettingsRejected("Poll interval must be at least 60 seconds");
        }

        if (cmd.ForecastDays is < 1 or > 16)
        {
            return new UpdateSettingsRejected("Forecast days must be between 1 and 16");
        }

        var projectedPolling = ConfigWriter.DeepClone(polling);
        if (cmd.PollInterval is { } interval)
        {
            projectedPolling.PollInterval = interval;
        }

        if (BudgetRejection(s => cmd.ApplyTo(s), scope, projectedPolling) is { } budgetRejection)
        {
            return new UpdateSettingsRejected(budgetRejection);
        }

        var failures = writer.Check<NjordOptions>(opt => cmd.ApplyTo(opt));
        if (failures.Count > 0)
        {
            return new UpdateSettingsRejected(string.Join("; ", failures));
        }

        var scopeResult = await writer.UpdateAsync<NjordOptions>(opt => cmd.ApplyTo(opt), ct);
        if (!scopeResult.Applied)
        {
            return new UpdateSettingsRejected(string.Join("; ", scopeResult.Failures));
        }

        if (cmd.PollInterval is not null)
        {
            var pollingResult = await writer.UpdateAsync<PollingOptions>(
                opt => opt.PollInterval = projectedPolling.PollInterval, ct);
            if (!pollingResult.Applied)
            {
                return new UpdateSettingsRejected(string.Join("; ", pollingResult.Failures));
            }
        }

        return new UpdateSettingsCompleted(currentConfig(), [.. RestartRequired(scopeResult, "")]);
    }

    public async Task<UpdateAnalysisConfigResponse> UpdateAnalysisConfigAsync(
        UpdateAnalysisConfig cmd, Func<EffectiveConfig> currentConfig, CancellationToken ct)
    {
        var updates = cmd.ToAnalysisUpdates();
        if (updates.Count == 0)
        {
            return new UpdateAnalysisConfigRejected(
                [new ConfigIssue(null, "At least one section or reset is required")], cmd.DryRun);
        }

        var result = await writer.UpdateManyAsync([.. updates.Select(u => u.Update)], cmd.DryRun, ct);
        if (!result.Valid)
        {
            return new UpdateAnalysisConfigRejected(
                [.. updates.Zip(result.Sections, (update, outcome) => (Update: update, Outcome: outcome)).SelectMany(pair =>
                    pair.Outcome.Failures.Select(message => new ConfigIssue(pair.Update.Section, message)))],
                cmd.DryRun);
        }

        var restartRequired = updates.Zip(result.Sections, (update, outcome) => (Update: update, Outcome: outcome)).SelectMany(pair =>
            pair.Outcome.RestartRequiredProperties.Select(p => $"{pair.Update.Prefix}{p}")).ToList();

        return cmd.DryRun
            ? new UpdateAnalysisConfigValidated(currentConfig(), restartRequired)
            : new UpdateAnalysisConfigCompleted(currentConfig(), restartRequired);
    }

    public async Task<UpdateBudgetResponse> UpdateBudgetAsync(
        UpdateBudget cmd, Func<EffectiveConfig> currentConfig, CancellationToken ct)
    {
        if (cmd.RequestsPerMonth is <= 0)
        {
            return new UpdateBudgetRejected("requests_per_month must be greater than zero");
        }

        if (cmd.RequestsPerMinute is <= 0)
        {
            return new UpdateBudgetRejected("requests_per_minute must be greater than zero");
        }

        var result = await writer.UpdateAsync<PollingOptions>(opt =>
        {
            if (cmd.RequestsPerMonth is not null || cmd.RequestsPerMinute is not null)
            {
                var current = BudgetCalculator.GetEffectiveBudget(opt);
                opt.BudgetOverride = new RequestBudget(
                    cmd.RequestsPerMonth ?? current.RequestsPerMonth,
                    cmd.RequestsPerMinute ?? current.RequestsPerMinute);
            }
            else
            {
                opt.BudgetOverride = null;
            }
        }, ct);

        return result.Applied
            ? new UpdateBudgetCompleted(currentConfig(), [])
            : new UpdateBudgetRejected(string.Join("; ", result.Failures));
    }

    private static string? BudgetRejection(Action<NjordOptions> applyScope, NjordOptions scope, PollingOptions polling)
    {
        var projected = ConfigWriter.DeepClone(scope);
        applyScope(projected);

        try
        {
            var projection = BudgetCalculator.Validate(projected, polling);
            return BudgetCalculator.GuardViolation(projection, polling.PollInterval) is not null
                ? $"Would exceed budget: {projection.UsagePercent:F0}% of monthly limit"
                : null;
        }
        catch (ParameterResolutionException ex)
        {
            return string.Join("; ", ex.Errors);
        }
    }

    private static IEnumerable<string> RestartRequired<T>(ConfigUpdateResult<T> result, string section)
        where T : class =>
        result.RequiresRestart
            ? result.ChangedProperties
                .Where(p => ConfigReloadPolicy.For<T>(p) == ReloadMode.RestartRequired)
                .Select(p => $"{section}{p}")
            : [];
}
