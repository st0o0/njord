using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Njord.Core.Configuration;
using Njord.Core.Domain;
using Njord.Messages.Pipeline;

namespace Njord.Core.Tests.Configuration;

public sealed class FeatureOptionsSpec
{
    private sealed class StubHandler(string name, FeatureCategory category, bool defaultEnabled) : IDomainHandler
    {
        public string Name { get; } = name;

        public FeatureDescriptor Feature { get; } = new(OutputKind.Condition, category, defaultEnabled, default);

        public IReadOnlyList<PipelineEvent> Handle(LocationForecast current, LocationForecast? previous, TimeProvider time) => [];
    }

    private static StubHandler Feature(string name, FeatureCategory category, bool defaultEnabled) =>
        new(name, category, defaultEnabled);

    private static FeatureOptionsValidator ValidatorFor(params IDomainHandler[] handlers)
    {
        var services = new ServiceCollection();
        foreach (var handler in handlers)
        {
            services.AddSingleton(handler);
        }

        return new FeatureOptionsValidator(services.BuildServiceProvider());
    }

    private static FeatureOptions Bind(Dictionary<string, string?> values)
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(values).Build();
        var services = new ServiceCollection();
        services.AddOptions<FeatureOptions>().Bind(configuration.GetSection(FeatureOptions.SectionName));
        using var provider = services.BuildServiceProvider();
        return provider.GetRequiredService<IOptions<FeatureOptions>>().Value;
    }

    [Fact]
    public void Should_follow_the_descriptor_default_without_an_entry()
    {
        var options = new FeatureOptions();

        Assert.Multiple(
            () => Assert.True(options.IsEnabled(Feature("Frost", FeatureCategory.Weather, defaultEnabled: true))),
            () => Assert.False(options.IsEnabled(Feature("FloodRisk", FeatureCategory.Flood, defaultEnabled: false))));
    }

    [Fact]
    public void Should_let_an_explicit_entry_override_the_default()
    {
        var options = new FeatureOptions();
        options.Set("Weather", "Frost", false);
        options.Set("Flood", "FloodRisk", true);

        Assert.Multiple(
            () => Assert.False(options.IsEnabled(Feature("Frost", FeatureCategory.Weather, defaultEnabled: true))),
            () => Assert.True(options.IsEnabled(Feature("FloodRisk", FeatureCategory.Flood, defaultEnabled: false))));
    }

    [Fact]
    public void Should_not_apply_an_entry_of_another_category()
    {
        var options = new FeatureOptions();
        options.Set("Lifestyle", "Frost", false);

        Assert.True(options.IsEnabled(Feature("Frost", FeatureCategory.Weather, defaultEnabled: true)));
    }

    [Fact]
    public void Should_list_the_configured_keys_as_category_and_name()
    {
        var options = new FeatureOptions();
        options.Set("Weather", "Frost", false);
        options.Set("Flood", "FloodRisk", true);

        Assert.Equal(["Weather:Frost", "Flood:FloodRisk"], options.ConfiguredKeys());
    }

    [Fact]
    public void Should_bind_features_hierarchically_from_the_features_section()
    {
        var options = Bind(new()
        {
            ["Njord:Features:Weather:Frost"] = "false",
            ["Njord:Features:Flood:FloodRisk"] = "true",
        });

        Assert.Multiple(
            () => Assert.Equal(2, options.Count),
            () => Assert.False(options["Weather"]["Frost"]),
            () => Assert.True(options["Flood"]["FloodRisk"]));
    }

    [Fact]
    public void Should_bind_keys_case_insensitively()
    {
        var options = Bind(new() { ["Njord:Features:weather:frost"] = "false" });

        Assert.False(options.IsEnabled(Feature("Frost", FeatureCategory.Weather, defaultEnabled: true)));
    }

    [Fact]
    public void Should_bind_environment_style_keys()
    {
        const string variable = "NJORDFEATURESPEC_Njord__Features__Lifestyle__Outdoor";
        Environment.SetEnvironmentVariable(variable, "true");
        try
        {
            var configuration = new ConfigurationBuilder().AddEnvironmentVariables("NJORDFEATURESPEC_").Build();
            var services = new ServiceCollection();
            services.AddOptions<FeatureOptions>().Bind(configuration.GetSection(FeatureOptions.SectionName));
            using var provider = services.BuildServiceProvider();

            var options = provider.GetRequiredService<IOptions<FeatureOptions>>().Value;

            Assert.True(options.IsEnabled(Feature("Outdoor", FeatureCategory.Lifestyle, defaultEnabled: false)));
        }
        finally
        {
            Environment.SetEnvironmentVariable(variable, null);
        }
    }

    [Fact]
    public void Should_accept_the_defaults()
    {
        var validator = ValidatorFor(Feature("Frost", FeatureCategory.Weather, true));

        Assert.True(validator.Validate(null, new FeatureOptions()).Succeeded);
    }

    [Fact]
    public void Should_reject_a_configuration_without_any_enabled_feature()
    {
        var validator = ValidatorFor(Feature("Frost", FeatureCategory.Weather, true), Feature("FloodRisk", FeatureCategory.Flood, false));
        var options = new FeatureOptions();
        options.Set("Weather", "Frost", false);

        var result = validator.Validate(null, options);

        Assert.Equal("At least one feature must be enabled.", ValidationAssert.SingleFailure(result));
    }

    [Fact]
    public void Should_reject_a_feature_without_a_registered_descriptor()
    {
        var validator = ValidatorFor(Feature("Frost", FeatureCategory.Weather, true));
        var options = new FeatureOptions();
        options.Set("Marine", "Surf", true);

        var result = validator.Validate(null, options);

        Assert.Equal("No feature is registered for: Marine:Surf.", ValidationAssert.SingleFailure(result));
    }

    [Fact]
    public void Should_reject_a_known_name_under_the_wrong_category()
    {
        var validator = ValidatorFor(Feature("Frost", FeatureCategory.Weather, true));
        var options = new FeatureOptions();
        options.Set("Lifestyle", "Frost", true);

        var result = validator.Validate(null, options);

        Assert.Equal("No feature is registered for: Lifestyle:Frost.", ValidationAssert.SingleFailure(result));
    }

    [Fact]
    public void Should_accept_configured_keys_regardless_of_case()
    {
        var validator = ValidatorFor(Feature("Frost", FeatureCategory.Weather, true));
        var options = new FeatureOptions();
        options.Set("weather", "FROST", true);

        Assert.True(validator.Validate(null, options).Succeeded);
    }
}
