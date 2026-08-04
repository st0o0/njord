using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Njord.Domain.Analysis;

namespace Njord.Configuration;

public sealed class ConsensusOptionsValidator : IValidateOptions<EnrichmentOptions>
{
    private static readonly HashSet<string> ValidMethods = new(StringComparer.OrdinalIgnoreCase)
        { "Mean", "Median", "TrimmedMean" };

    public ValidateOptionsResult Validate(string? name, EnrichmentOptions options)
    {
        var c = options.Consensus;

        if (!ValidMethods.Contains(c.Method))
        {
            return ValidateOptionsResult.Fail(
                $"Consensus.Method '{c.Method}' is invalid. Valid values: {string.Join(", ", ValidMethods)}");
        }

        if (string.Equals(c.Method, "TrimmedMean", StringComparison.OrdinalIgnoreCase)
            && c.TrimPercent is <= 0 or >= 0.5)
        {
            return ValidateOptionsResult.Fail(
                $"Consensus.TrimPercent must be between 0 and 0.5 (exclusive) when Method is TrimmedMean, got {c.TrimPercent}");
        }

        return ValidateOptionsResult.Success;
    }
}

public sealed class EnergyOptionsValidator : IValidateOptions<EnrichmentOptions>
{
    public ValidateOptionsResult Validate(string? name, EnrichmentOptions options)
    {
        var e = options.Energy;

        if (e.CarnotEfficiency is <= 0 or >= 1)
        {
            return ValidateOptionsResult.Fail(
                $"Energy.CarnotEfficiency must be between 0 and 1 (exclusive), got {e.CarnotEfficiency}");
        }

        if (e.FlowTemp <= 0)
        {
            return ValidateOptionsResult.Fail(
                $"Energy.FlowTemp must be positive, got {e.FlowTemp}");
        }

        return ValidateOptionsResult.Success;
    }
}

public sealed class HistoryOptionsValidator : IValidateOptions<EnrichmentOptions>
{
    public ValidateOptionsResult Validate(string? name, EnrichmentOptions options)
    {
        var h = options.History;
        var errors = new List<string>();

        if (h.SnapshotInterval <= 0)
        {
            errors.Add($"History.SnapshotInterval must be positive, got {h.SnapshotInterval}");
        }

        if (h.RetentionDays <= 0)
        {
            errors.Add($"History.RetentionDays must be positive, got {h.RetentionDays}");
        }

        if (h.MinSampleSize <= 0)
        {
            errors.Add($"History.MinSampleSize must be positive, got {h.MinSampleSize}");
        }

        return errors.Count > 0
            ? ValidateOptionsResult.Fail(errors)
            : ValidateOptionsResult.Success;
    }
}

public sealed class IndexOptionsValidator : IValidateOptions<EnrichmentOptions>
{
    private static readonly HashSet<string> ValidScoreNames =
        new(PreferenceResolver.ScoreNames, StringComparer.OrdinalIgnoreCase);

    private readonly ILogger<IndexOptionsValidator> _logger;
    private readonly IOptions<NjordOptions> _njordOptions;

    public IndexOptionsValidator(ILogger<IndexOptionsValidator> logger, IOptions<NjordOptions> njordOptions)
    {
        _logger = logger;
        _njordOptions = njordOptions;
    }

    public ValidateOptionsResult Validate(string? name, EnrichmentOptions options)
    {
        var idx = options.Indices;

        ClampPreferences(idx.Preferences);

        foreach (var (scoreName, prefs) in idx.ScoreOverrides)
        {
            if (!ValidScoreNames.Contains(scoreName))
            {
                _logger.LogWarning("Indices.ScoreOverrides contains unknown score name '{ScoreName}', ignoring", scoreName);
            }

            ClampPreferences(prefs);
        }

        var knownLocations = new HashSet<string>(
            _njordOptions.Value.Locations.Select(l => l.Name),
            StringComparer.OrdinalIgnoreCase);

        foreach (var loc in idx.LocationOverrides)
        {
            if (!knownLocations.Contains(loc.Location))
            {
                _logger.LogWarning(
                    "Indices.LocationOverrides contains unknown location '{Location}', ignoring",
                    loc.Location);
            }

            ClampPreferences(loc.Preferences);
            foreach (var (scoreName, prefs) in loc.ScoreOverrides)
            {
                if (!ValidScoreNames.Contains(scoreName))
                {
                    _logger.LogWarning(
                        "Indices.LocationOverrides[{Location}].ScoreOverrides contains unknown score name '{ScoreName}', ignoring",
                        loc.Location, scoreName);
                }

                ClampPreferences(prefs);
            }
        }

        return ValidateOptionsResult.Success;
    }

    private static void ClampPreferences(IndexPreferences prefs)
    {
        if (prefs.HeatSensitivity is { } hs)
            prefs.HeatSensitivity = Math.Clamp(hs, 0.0, 5.0);
        if (prefs.HumiditySensitivity is { } hms)
            prefs.HumiditySensitivity = Math.Clamp(hms, 0.0, 5.0);
        if (prefs.WindSensitivity is { } ws)
            prefs.WindSensitivity = Math.Clamp(ws, 0.0, 5.0);
        if (prefs.RainSensitivity is { } rs)
            prefs.RainSensitivity = Math.Clamp(rs, 0.0, 5.0);
    }
}
