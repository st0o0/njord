using System.Reflection;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Njord.Core.Configuration;
using Njord.Core.Configuration.Alerts;
using Njord.Core.Configuration.Consensus;
using Njord.Core.Configuration.Derived;
using Njord.Core.Configuration.Indices;
using Njord.Core.Configuration.Mqtt;
using Njord.Core.Configuration.Polling;
using Njord.Core.Configuration.Trends;

namespace Njord.Tests.Configuration;

public sealed class OptionsBindingSpec : IDisposable
{
    private readonly string _tempDir = Path.Combine(Path.GetTempPath(), $"njord-binding-{Guid.NewGuid():N}");

    private static IEnumerable<Type> SectionOptionsTypes() =>
        typeof(NjordOptions).Assembly.GetTypes()
            .Where(t => t is { IsClass: true, IsAbstract: false } && t.Name.EndsWith("Options", StringComparison.Ordinal))
            .Where(t => t.GetField("SectionName", BindingFlags.Public | BindingFlags.Static) is { IsLiteral: true });

    private ServiceProvider BuildHost(IDictionary<string, string?>? overrides = null) =>
        HostConfiguration.Services(HostConfiguration.Sample(_tempDir, overrides)).BuildServiceProvider();

    [Fact]
    public void Every_section_options_type_resolves_from_the_sample_configuration()
    {
        using var provider = BuildHost();

        foreach (var type in SectionOptionsTypes())
        {
            var options = provider.GetRequiredService(typeof(IOptions<>).MakeGenericType(type));
            var value = options.GetType().GetProperty(nameof(IOptions<object>.Value))!.GetValue(options);
            Assert.NotNull(value);
        }
    }

    [Fact]
    public void The_sample_configuration_passes_startup_validation()
    {
        using var provider = BuildHost();

        provider.GetRequiredService<IStartupValidator>().Validate();
    }

    [Fact]
    public void Every_section_options_type_is_bound_exactly_once_with_exactly_one_validator()
    {
        var services = HostConfiguration.Services(HostConfiguration.Sample(_tempDir));

        foreach (var type in SectionOptionsTypes())
        {
            var bindings = services.Count(d =>
                d.ServiceType == typeof(IOptionsChangeTokenSource<>).MakeGenericType(type));
            var validators = services.Count(d =>
                d.ServiceType == typeof(IValidateOptions<>).MakeGenericType(type));
            Assert.True(bindings == 1, $"{type.Name} is bound {bindings} times");
            Assert.True(validators == 1, $"{type.Name} has {validators} validators");
        }
    }

    [Fact]
    public void No_aggregate_enrichment_options_is_registered()
    {
        var services = HostConfiguration.Services(HostConfiguration.Sample(_tempDir));

        Assert.DoesNotContain(services, d =>
            d.ServiceType.GenericTypeArguments.Any(t => t.Name == "EnrichmentOptions"));
    }

    [Fact]
    public void Existing_keys_bind_to_the_split_options_types()
    {
        using var provider = BuildHost(new Dictionary<string, string?>
        {
            ["Njord:PollInterval"] = "00:30:00",
            ["Njord:Mqtt:Host"] = "broker.local",
            ["Njord:Enrichment:Alerts:Enabled"] = "false",
            ["Njord:Enrichment:Indices:Enabled"] = "true",
            ["Njord:Enrichment:Trends:Enabled"] = "false",
            ["Njord:Enrichment:Derived:Enabled"] = "true",
            ["Njord:Enrichment:Consensus:MinQuorum"] = "3",
        });

        Assert.Equal(TimeSpan.FromMinutes(30), provider.GetRequiredService<IOptions<PollingOptions>>().Value.PollInterval);
        Assert.Equal("broker.local", provider.GetRequiredService<IOptions<MqttOptions>>().Value.Host);
        Assert.False(provider.GetRequiredService<IOptions<AlertOptions>>().Value.Enabled);
        Assert.True(provider.GetRequiredService<IOptions<IndexOptions>>().Value.Enabled);
        Assert.False(provider.GetRequiredService<IOptions<TrendOptions>>().Value.Enabled);
        Assert.True(provider.GetRequiredService<IOptions<DerivedOptions>>().Value.Enabled);
        Assert.Equal(3, provider.GetRequiredService<IOptions<ConsensusOptions>>().Value.MinQuorum);
    }

    [Fact]
    public void Configured_threshold_arrays_replace_the_defaults()
    {
        using var provider = BuildHost(new Dictionary<string, string?>
        {
            ["Njord:Enrichment:Alerts:FrostThresholds:0"] = "1",
            ["Njord:Enrichment:Alerts:FrostThresholds:1"] = "-4",
            ["Njord:Enrichment:Alerts:FrostThresholds:2"] = "-12",
        });

        Assert.Equal([1.0, -4.0, -12.0], provider.GetRequiredService<IOptions<AlertOptions>>().Value.FrostThresholds);
    }

    [Fact]
    public void An_invalid_section_fails_startup_validation_naming_the_problem()
    {
        using var provider = BuildHost(new Dictionary<string, string?>
        {
            ["Njord:Enrichment:Consensus:MinQuorum"] = "0",
        });

        var ex = Assert.Throws<OptionsValidationException>(() => provider.GetRequiredService<IStartupValidator>().Validate());
        Assert.Contains("MinQuorum", ex.Message);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
        {
            Directory.Delete(_tempDir, recursive: true);
        }
    }
}
