using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Njord.Core.Configuration;
using Njord.Core.Configuration.Alerts;

namespace Njord.Core.Tests.Configuration;

public sealed class SampleOptions
{
    public const string SectionName = "Njord:Sample";

    public IList<int> Steps { get; set; } = [1, 2, 3];

    public int Limit { get; set; } = 10;
}

public sealed class SampleOptionsValidator : IValidateOptions<SampleOptions>
{
    public ValidateOptionsResult Validate(string? name, SampleOptions options) =>
        options.Limit > 0 ? ValidateOptionsResult.Success : ValidateOptionsResult.Fail("Limit must be positive.");
}

public sealed class UnvalidatedOptions
{
    public const string SectionName = "Njord:Unvalidated";
}

public sealed class RelaxedSampleValidator : IValidateOptions<SampleOptions>
{
    public ValidateOptionsResult Validate(string? name, SampleOptions options) => ValidateOptionsResult.Success;
}

public sealed class OptionsRegistrationSpec
{
    private static IConfigurationRoot Configuration(params (string Key, string Value)[] entries) =>
        new ConfigurationBuilder()
            .AddInMemoryCollection(entries.ToDictionary(e => e.Key, e => (string?)e.Value))
            .Build();

    private static ServiceProvider Build(IConfiguration configuration, Action<IServiceCollection, IConfiguration>? register = null)
    {
        var services = new ServiceCollection();
        services.AddSingleton(configuration);
        (register ?? ((s, c) => s.AddNjordOptions<SampleOptions>(c)))(services, configuration);
        return services.BuildServiceProvider();
    }

    [Fact]
    public void The_options_bind_to_the_section_named_by_the_type()
    {
        using var provider = Build(Configuration(("Njord:Sample:Limit", "25")));

        Assert.Equal(25, provider.GetRequiredService<IOptions<SampleOptions>>().Value.Limit);
    }

    [Fact]
    public void A_configured_list_replaces_the_default_list()
    {
        using var provider = Build(Configuration(("Njord:Sample:Steps:0", "7"), ("Njord:Sample:Steps:1", "9")));

        Assert.Equal([7, 9], provider.GetRequiredService<IOptions<SampleOptions>>().Value.Steps);
    }

    [Fact]
    public void An_unconfigured_list_keeps_its_default()
    {
        using var provider = Build(Configuration(("Njord:Sample:Limit", "25")));

        Assert.Equal([1, 2, 3], provider.GetRequiredService<IOptions<SampleOptions>>().Value.Steps);
    }

    [Fact]
    public void The_conventional_validator_is_registered()
    {
        var services = new ServiceCollection();

        services.AddNjordOptions<SampleOptions>(Configuration());

        var descriptor = Assert.Single(services, d => d.ServiceType == typeof(IValidateOptions<SampleOptions>));
        Assert.Equal(typeof(SampleOptionsValidator), descriptor.ImplementationType);
    }

    [Fact]
    public void An_explicit_validator_overrides_the_convention()
    {
        var services = new ServiceCollection();

        services.AddNjordOptions<SampleOptions, RelaxedSampleValidator>(Configuration());

        var descriptor = Assert.Single(services, d => d.ServiceType == typeof(IValidateOptions<SampleOptions>));
        Assert.Equal(typeof(RelaxedSampleValidator), descriptor.ImplementationType);
    }

    [Fact]
    public void An_invalid_value_fails_startup_validation_with_the_validator_message()
    {
        using var provider = Build(Configuration(("Njord:Sample:Limit", "0")));

        var ex = Assert.Throws<OptionsValidationException>(() => provider.GetRequiredService<IStartupValidator>().Validate());

        Assert.Equal(["Limit must be positive."], ex.Failures);
    }

    [Fact]
    public void A_type_without_a_conventional_validator_is_rejected()
    {
        var services = new ServiceCollection();

        var ex = Assert.Throws<InvalidOperationException>(() => services.AddNjordOptions<UnvalidatedOptions>(Configuration()));

        Assert.Equal(
            "No validator 'UnvalidatedOptionsValidator' implementing IValidateOptions<UnvalidatedOptions> "
            + "was found in namespace 'Njord.Core.Tests.Configuration'.",
            ex.Message);
    }

    [Fact]
    public void Shipped_options_register_their_validator_by_convention()
    {
        var services = new ServiceCollection();

        services.AddNjordOptions<AlertOptions>(Configuration());

        var descriptor = Assert.Single(services, d => d.ServiceType == typeof(IValidateOptions<AlertOptions>));
        Assert.Equal(typeof(AlertOptionsValidator), descriptor.ImplementationType);
    }

    [Fact]
    public void Changes_to_the_configuration_reach_a_monitor()
    {
        var configuration = Configuration(("Njord:Sample:Limit", "25"));
        using var provider = Build(configuration);
        var monitor = provider.GetRequiredService<IOptionsMonitor<SampleOptions>>();

        configuration["Njord:Sample:Limit"] = "30";
        configuration.Reload();

        Assert.Equal(30, monitor.CurrentValue.Limit);
    }
}
