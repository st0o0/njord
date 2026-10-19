using System.Reflection;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Njord.Alerts.Configuration;
using Njord.Core.Configuration;
using Njord.Core.Configuration.Mqtt;
using Njord.Core.Configuration.OpenMeteo;
using Njord.Core.Configuration.Persistence;
using Njord.Core.Configuration.Polling;
using Njord.Pipeline.Configuration;

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
            var property = options.GetType().GetProperty(nameof(IOptions<object>.Value));
            Assert.NotNull(property);
            var value = property.GetValue(options);
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
            ["Njord:Polling:PollInterval"] = "00:30:00",
            ["Njord:Mqtt:Host"] = "broker.local",
            ["Njord:Enrichment:Alerts:Enabled"] = "false",
        });

        Assert.Equal(TimeSpan.FromMinutes(30), provider.GetRequiredService<IOptions<PollingOptions>>().Value.PollInterval);
        Assert.Equal("broker.local", provider.GetRequiredService<IOptions<MqttOptions>>().Value.Host);
        Assert.False(provider.GetRequiredService<IOptions<AlertOptions>>().Value.Enabled);
    }

    [Fact]
    public void The_per_concern_keys_bind_to_their_own_sections()
    {
        using var provider = BuildHost(new Dictionary<string, string?>
        {
            ["Njord:Polling:DiscoveryInterval"] = "00:10:00",
            ["Njord:Polling:BudgetOverride:RequestsPerMonth"] = "100000",
            ["Njord:Polling:BudgetOverride:RequestsPerMinute"] = "200",
            ["Njord:OpenMeteo:BaseUrl"] = "http://localhost:8080",
            ["Njord:Storage:PersistencePath"] = "data/other.db",
        });

        var polling = provider.GetRequiredService<IOptions<PollingOptions>>().Value;
        Assert.Multiple(
            () => Assert.Equal(TimeSpan.FromMinutes(10), polling.DiscoveryInterval),
            () => Assert.Equal(new RequestBudget(100_000, 200), polling.BudgetOverride),
            () => Assert.Equal("http://localhost:8080", provider.GetRequiredService<IOptions<OpenMeteoOptions>>().Value.BaseUrl),
            () => Assert.Equal("data/other.db", provider.GetRequiredService<IOptions<StorageOptions>>().Value.PersistencePath));
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
            ["Njord:Enrichment:Alerts:FrostThresholds:0"] = "-9",
            ["Njord:Enrichment:Alerts:FrostThresholds:1"] = "5",
            ["Njord:Enrichment:Alerts:FrostThresholds:2"] = "9",
        });

        var ex = Assert.Throws<OptionsValidationException>(() => provider.GetRequiredService<IStartupValidator>().Validate());
        Assert.Contains("FrostThresholds", ex.Message);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
        {
            Directory.Delete(_tempDir, recursive: true);
        }
    }
}
