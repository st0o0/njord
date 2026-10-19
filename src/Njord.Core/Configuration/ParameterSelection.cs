
namespace Njord.Core.Configuration;

public static class ParameterSelection
{
    private static readonly string ValidGroups =
        string.Join(", ", nameof(ParameterGroup.Weather), nameof(ParameterGroup.Solar), nameof(ParameterGroup.Soil));

    public static ResolvedParameterSet Resolve(
        IEnumerable<string> groups,
        IEnumerable<string> extra,
        IEnumerable<string> exclude)
    {
        var errors = new List<string>();

        var parsedGroups = new List<ParameterGroup>();
        foreach (var g in groups)
        {
            if (TryParseGroup(g, out var pg))
            {
                parsedGroups.Add(pg);
            }
            else
            {
                errors.Add($"Unknown parameter group: '{g}'. Valid groups: {ValidGroups}");
            }
        }

        var extraParams = new List<ParameterDef>();
        foreach (var name in extra)
        {
            if (ParameterRegistry.GetByApiName(name) is { } p)
            {
                extraParams.Add(p);
            }
            else
            {
                errors.Add($"Unknown parameter in Extra: '{name}'");
            }
        }

        var excludeSet = new HashSet<string>(StringComparer.Ordinal);
        foreach (var name in exclude)
        {
            if (ParameterRegistry.GetByApiName(name) is not null)
            {
                excludeSet.Add(name);
            }
            else
            {
                errors.Add($"Unknown parameter in Exclude: '{name}'");
            }
        }

        if (errors.Count > 0)
        {
            throw new ParameterResolutionException(errors);
        }

        var resolved = parsedGroups
            .SelectMany(ParameterRegistry.GetByGroup)
            .Concat(extraParams)
            .Where(p => !excludeSet.Contains(p.ApiName))
            .Distinct()
            .ToList();

        if (resolved.Count == 0)
        {
            throw new ParameterResolutionException(["The resolved parameter set is empty. Enable at least one group or add parameters via Extra."]);
        }

        return new ResolvedParameterSet(
            [.. resolved.Where(p => p.Granularity == ParameterGranularity.Hourly)],
            [.. resolved.Where(p => p.Granularity == ParameterGranularity.Daily)]);
    }

    private static bool TryParseGroup(string text, out ParameterGroup group)
    {
        if (Matches(text, nameof(ParameterGroup.Weather)))
        {
            group = ParameterGroup.Weather;
            return true;
        }

        if (Matches(text, nameof(ParameterGroup.Solar)))
        {
            group = ParameterGroup.Solar;
            return true;
        }

        if (Matches(text, nameof(ParameterGroup.Soil)))
        {
            group = ParameterGroup.Soil;
            return true;
        }

        group = default;
        return false;
    }

    private static bool Matches(string text, string name) =>
        string.Equals(text, name, StringComparison.OrdinalIgnoreCase);
}
