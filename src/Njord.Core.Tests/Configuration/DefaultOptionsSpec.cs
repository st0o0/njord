using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using Njord.Core.Configuration;
using Njord.Core.Configuration.Alerts;
using Njord.Core.Configuration.Consensus;
using Njord.Core.Configuration.Derived;
using Njord.Core.Configuration.Indices;
using Njord.Core.Configuration.Mqtt;
using Njord.Core.Configuration.OpenMeteo;
using Njord.Core.Configuration.Persistence;
using Njord.Core.Configuration.Polling;
using Njord.Core.Configuration.Sensors;
using Njord.Core.Configuration.Trends;
using Njord.Tests.Shared;
using static VerifyXunit.Verifier;

namespace Njord.Core.Tests.Configuration;

public sealed class DefaultOptionsSpec
{
    private static readonly JsonSerializerOptions Json = new()
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() },
    };

    [Fact]
    public Task Default_options_match_golden_master() =>
        Verify(JsonSerializer.Serialize(new
        {
            Njord = AppSettingsDefaults.Options<NjordOptions>(),
            Polling = AppSettingsDefaults.Options<PollingOptions>(),
            OpenMeteo = AppSettingsDefaults.Options<OpenMeteoOptions>(),
            Storage = AppSettingsDefaults.Options<StorageOptions>(),
            Persistence = AppSettingsDefaults.Options<PersistenceOptions>(),
            Mqtt = AppSettingsDefaults.Options<MqttOptions>(),
            Sensors = AppSettingsDefaults.Options<SensorOptions>(),
            Consensus = AppSettingsDefaults.Options<ConsensusOptions>(),
            Alerts = AppSettingsDefaults.Options<AlertOptions>(),
            Derived = AppSettingsDefaults.Options<DerivedOptions>(),
            Trends = AppSettingsDefaults.Options<TrendOptions>(),
            Indices = AppSettingsDefaults.Options<IndexOptions>(),
        }, Json), "json");

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
