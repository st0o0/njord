using System.Text.RegularExpressions;
using Microsoft.Extensions.DependencyInjection;
using Njord.Advisories;
using Njord.Alerts;
using Njord.Core.Configuration;
using Njord.Core.Domain;
using Njord.Insights;
using Njord.Scores;
using Njord.Tests.Shared;

namespace Njord.Architecture.Tests;

public sealed partial class FeatureConfigKeySpec
{
    [GeneratedRegex("^[A-Z][A-Za-z0-9]*$")]
    private static partial Regex PascalCase();

    private static IReadOnlyList<IFeature> RegisteredFeatures()
    {
        using var provider = FeatureServices.Build(TimeProvider.System, services =>
        {
            services.AddAlertOptions(AppSettingsDefaults.Configuration());
            services.AddAlertHandlers().AddScoreHandlers().AddInsightHandlers().AddAdvisoryHandlers();
        });
        return FeatureResolution.AllFeatures(provider.GetServices<IDomainHandler>());
    }

    [Fact]
    public void Every_registered_feature_has_a_distinct_name()
    {
        var names = RegisteredFeatures().Select(feature => feature.Name).ToList();

        Assert.Multiple(
            () => Assert.NotEmpty(names),
            () => Assert.Equal(names.Count, names.Distinct(StringComparer.OrdinalIgnoreCase).Count()));
    }

    [Fact]
    public void Every_registered_feature_has_a_distinct_wire_key()
    {
        var keys = RegisteredFeatures().Select(feature => feature.WireKey()).ToList();

        Assert.Equal(keys.Count, keys.Distinct(StringComparer.Ordinal).Count());
    }

    [Fact]
    public void Every_registered_feature_has_a_distinct_section_key()
    {
        var keys = RegisteredFeatures().Select(feature => feature.SectionKey()).ToList();

        Assert.Equal(keys.Count, keys.Distinct(StringComparer.OrdinalIgnoreCase).Count());
    }

    [Fact]
    public void Every_registered_feature_name_is_pascal_case_alphanumeric()
    {
        var invalid = RegisteredFeatures().Select(feature => feature.Name).Where(name => !PascalCase().IsMatch(name)).ToList();

        Assert.Empty(invalid);
    }
}
