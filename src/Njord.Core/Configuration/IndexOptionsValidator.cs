using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Njord.Compute.Analysis;
using Njord.Compute.Configuration;

namespace Njord.Core.Configuration;

public sealed class IndexOptionsValidator : IValidateOptions<EnrichmentOptions>
{
    private static readonly HashSet<string> ValidScoreNames =
        new(PreferenceResolver.ScoreNames, StringComparer.OrdinalIgnoreCase);

    private readonly ILogger<IndexOptionsValidator> _logger;

    public IndexOptionsValidator(ILogger<IndexOptionsValidator> logger)
    {
        _logger = logger;
    }

    public ValidateOptionsResult Validate(string? name, EnrichmentOptions options)
    {
        var idx = options.Indices;
        var errors = new List<string>();

        ValidatePreferences(idx.Preferences, "Indices.Preferences", errors);

        foreach (var (scoreName, prefs) in idx.ScoreOverrides)
        {
            if (!ValidScoreNames.Contains(scoreName))
            {
                _logger.LogWarning("Indices.ScoreOverrides contains unknown score name '{ScoreName}', ignoring", scoreName);
            }

            ValidatePreferences(prefs, $"Indices.ScoreOverrides[{scoreName}]", errors);
        }

        foreach (var loc in idx.LocationOverrides)
        {
            ValidatePreferences(loc.Preferences, $"Indices.LocationOverrides[{loc.Location}]", errors);
            foreach (var (scoreName, prefs) in loc.ScoreOverrides)
            {
                if (!ValidScoreNames.Contains(scoreName))
                {
                    _logger.LogWarning(
                        "Indices.LocationOverrides[{Location}].ScoreOverrides contains unknown score name '{ScoreName}', ignoring",
                        loc.Location, scoreName);
                }

                ValidatePreferences(prefs, $"Indices.LocationOverrides[{loc.Location}].ScoreOverrides[{scoreName}]", errors);
            }
        }

        return errors.Count > 0
            ? ValidateOptionsResult.Fail(errors)
            : ValidateOptionsResult.Success;
    }

    private static void ValidatePreferences(IndexPreferences prefs, string path, List<string> errors)
    {
        ValidateRange(prefs.HeatSensitivity, nameof(IndexPreferences.HeatSensitivity), path, 0.0, 5.0, errors);
        ValidateRange(prefs.HumiditySensitivity, nameof(IndexPreferences.HumiditySensitivity), path, 0.0, 5.0, errors);
        ValidateRange(prefs.WindSensitivity, nameof(IndexPreferences.WindSensitivity), path, 0.0, 5.0, errors);
        ValidateRange(prefs.RainSensitivity, nameof(IndexPreferences.RainSensitivity), path, 0.0, 5.0, errors);
    }

    private static void ValidateRange(double? value, string propertyName, string path, double min, double max, List<string> errors)
    {
        if (value is { } v && (v < min || v > max))
        {
            errors.Add($"{path}.{propertyName} must be between {min:0.0} and {max:0.0}, got {v}");
        }
    }
}
