using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using Njord.Alerts.Configuration;
using Njord.Core.Configuration;
using Njord.Core.Configuration.Health;
using Njord.Core.Configuration.Mqtt;
using Njord.Core.Configuration.OpenMeteo;
using Njord.Core.Configuration.Persistence;
using Njord.Core.Configuration.Polling;
using Njord.Core.Configuration.Sensors;
using Njord.Derived.Configuration;
using Njord.Indices.Configuration;
using Njord.Pipeline.Configuration;
using Njord.Tests.Shared;
using Njord.Trends.Configuration;

namespace Njord.Tests.Configuration;

public sealed class DefaultOptionsSpec
{
    [Fact]
    public void A_scalar_override_wins_over_the_file_and_a_configured_list_replaces_its_default()
    {
        var configuration = new ConfigurationBuilder()
            .AddJsonFile(AppSettingsDefaults.ShippedPath, optional: false)
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Njord:Polling:PollInterval"] = "00:30:00",
                ["Njord:Horizons:0"] = "6",
                ["Njord:Horizons:1"] = "24",
            })
            .Build();

        var polling = AppSettingsDefaults.Options<PollingOptions>(configuration);
        var njord = AppSettingsDefaults.Options<NjordOptions>(configuration);

        Assert.Multiple(
            () => Assert.Equal(TimeSpan.FromMinutes(30), polling.PollInterval),
            () => Assert.Equal(TimeSpan.FromMinutes(20), polling.DiscoveryInterval),
            () => Assert.Equal([6, 24], njord.Horizons));
    }

    [Fact]
    public void The_shipped_file_binds_and_passes_every_validator()
    {
        // Locations and models are deployment-specific and deliberately absent from the shipped file.
        var configuration = new ConfigurationBuilder()
            .AddJsonFile(AppSettingsDefaults.ShippedPath, optional: false)
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Njord:Locations:0:Name"] = "luzern",
                ["Njord:Locations:0:Latitude"] = "47.0502",
                ["Njord:Locations:0:Longitude"] = "8.3093",
                ["Njord:Models:0"] = "icon_global",
            })
            .Build();

        var results = new Dictionary<string, ValidateOptionsResult>
        {
            [nameof(NjordOptions)] = new NjordOptionsValidator(configuration).Validate(null, AppSettingsDefaults.Options<NjordOptions>(configuration)),
            [nameof(PollingOptions)] = new PollingOptionsValidator(configuration).Validate(null, AppSettingsDefaults.Options<PollingOptions>(configuration)),
            [nameof(HealthOptions)] = new HealthOptionsValidator().Validate(null, AppSettingsDefaults.Options<HealthOptions>(configuration)),
            [nameof(OpenMeteoOptions)] = new OpenMeteoOptionsValidator().Validate(null, AppSettingsDefaults.Options<OpenMeteoOptions>(configuration)),
            [nameof(StorageOptions)] = new StorageOptionsValidator(configuration).Validate(null, AppSettingsDefaults.Options<StorageOptions>(configuration)),
            [nameof(PersistenceOptions)] = new PersistenceOptionsValidator().Validate(null, AppSettingsDefaults.Options<PersistenceOptions>(configuration)),
            [nameof(MqttOptions)] = new MqttOptionsValidator().Validate(null, AppSettingsDefaults.Options<MqttOptions>(configuration)),
            [nameof(SensorOptions)] = new SensorOptionsValidator().Validate(null, AppSettingsDefaults.Options<SensorOptions>(configuration)),
            [nameof(ConsensusOptions)] = new ConsensusOptionsValidator().Validate(null, AppSettingsDefaults.Options<ConsensusOptions>(configuration)),
            [nameof(AlertOptions)] = new AlertOptionsValidator().Validate(null, AppSettingsDefaults.Options<AlertOptions>(configuration)),
            [nameof(DerivedOptions)] = new DerivedOptionsValidator().Validate(null, AppSettingsDefaults.Options<DerivedOptions>(configuration)),
            [nameof(TrendOptions)] = new TrendOptionsValidator().Validate(null, AppSettingsDefaults.Options<TrendOptions>(configuration)),
            [nameof(IndexOptions)] = new IndexOptionsValidator().Validate(null, AppSettingsDefaults.Options<IndexOptions>(configuration)),
        };

        Assert.Multiple(results.Select(r => (Action)(() =>
            Assert.True(r.Value.Succeeded, $"{r.Key}: {string.Join("; ", r.Value.Failures ?? [])}"))).ToArray());
    }
}
