using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Njord.Core.Configuration;
using Njord.Core.Domain;
using Njord.Messages.Pipeline;

namespace Njord.Core.Tests.Configuration;

public sealed class FeatureOptionsSpec
{
    private sealed class StubHandler(FeatureKind kind, bool defaultEnabled) : IDomainHandler
    {
        public FeatureDescriptor Feature { get; } = new(kind, FeatureCategory.Lifestyle, "stub", defaultEnabled, default);

        public IReadOnlyList<PipelineEvent> Handle(LocationForecast current, LocationForecast? previous, TimeProvider time) => [];
    }

    private static FeatureDescriptor Descriptor(FeatureKind kind, bool defaultEnabled) =>
        new(kind, FeatureCategory.Lifestyle, "stub", defaultEnabled, default);

    private static FeatureOptionsValidator ValidatorFor(params IDomainHandler[] handlers)
    {
        var services = new ServiceCollection();
        foreach (var handler in handlers)
        {
            services.AddSingleton(handler);
        }

        return new FeatureOptionsValidator(services.BuildServiceProvider());
    }

    [Fact]
    public void Should_follow_the_descriptor_default_without_an_entry()
    {
        var options = new FeatureOptions();

        Assert.Multiple(
            () => Assert.True(options.IsEnabled(Descriptor(FeatureKind.FrostAlert, defaultEnabled: true))),
            () => Assert.False(options.IsEnabled(Descriptor(FeatureKind.FloodRisk, defaultEnabled: false))));
    }

    [Fact]
    public void Should_let_an_explicit_entry_override_the_default()
    {
        var options = new FeatureOptions { [FeatureKind.FrostAlert] = false, [FeatureKind.FloodRisk] = true };

        Assert.Multiple(
            () => Assert.False(options.IsEnabled(Descriptor(FeatureKind.FrostAlert, defaultEnabled: true))),
            () => Assert.True(options.IsEnabled(Descriptor(FeatureKind.FloodRisk, defaultEnabled: false))));
    }

    [Fact]
    public void Should_bind_feature_names_from_the_features_section()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Njord:Features:FrostAlert"] = "false",
                ["Njord:Features:FloodRisk"] = "true",
            })
            .Build();
        var services = new ServiceCollection();
        services.AddOptions<FeatureOptions>().Bind(configuration.GetSection(FeatureOptions.SectionName));
        using var provider = services.BuildServiceProvider();

        var options = provider.GetRequiredService<IOptions<FeatureOptions>>().Value;

        Assert.Multiple(
            () => Assert.Equal(2, options.Count),
            () => Assert.False(options[FeatureKind.FrostAlert]),
            () => Assert.True(options[FeatureKind.FloodRisk]));
    }

    [Fact]
    public void Should_accept_the_defaults()
    {
        var validator = ValidatorFor(new StubHandler(FeatureKind.FrostAlert, true));

        Assert.True(validator.Validate(null, new FeatureOptions()).Succeeded);
    }

    [Fact]
    public void Should_reject_a_configuration_without_any_enabled_feature()
    {
        var validator = ValidatorFor(new StubHandler(FeatureKind.FrostAlert, true), new StubHandler(FeatureKind.FloodRisk, false));
        var options = new FeatureOptions { [FeatureKind.FrostAlert] = false };

        var result = validator.Validate(null, options);

        Assert.Equal("At least one feature must be enabled.", ValidationAssert.SingleFailure(result));
    }

    [Fact]
    public void Should_reject_a_feature_without_a_registered_descriptor()
    {
        var validator = ValidatorFor(new StubHandler(FeatureKind.FrostAlert, true));
        var options = new FeatureOptions { [FeatureKind.SurfConditions] = true };

        var result = validator.Validate(null, options);

        Assert.Equal("No feature is registered for: SurfConditions.", ValidationAssert.SingleFailure(result));
    }
}
